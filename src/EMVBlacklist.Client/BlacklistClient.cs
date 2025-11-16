using System.Net.Http.Json;
using System.Text.Json;
using EMVBlacklist.Shared.Filters;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Client;

public class BlacklistClient
{
    private readonly HttpClient _httpClient;
    private IBlacklistFilter? _filter;
    private long _lastTimestamp;
    private readonly Timer _syncTimer;
    private int _totalDeltasReceived;
    private int _totalAdded;
    private int _totalRemoved;

    public BlacklistClient(string apiUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiUrl)
        };

        _syncTimer = new Timer(SyncDeltas, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task InitializeAsync(FilterType? filterType = null)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("EMV Blacklist Client - Starting");
        Console.WriteLine("===========================================");

        try
        {
            // Wait for API to be ready
            Console.WriteLine("Waiting for API to be ready...");
            await WaitForApiAsync();

            // Get initial load
            Console.WriteLine($"\nRequesting initial load{(filterType.HasValue ? $" with filter: {filterType.Value}" : "")}...");
            var url = filterType.HasValue
                ? $"/api/blacklist/initial?filterType={filterType.Value}"
                : "/api/blacklist/initial";

            var response = await _httpClient.GetFromJsonAsync<InitialLoadResponse>(url);

            if (response == null)
            {
                throw new Exception("Failed to get initial load");
            }

            Console.WriteLine($"\nInitial load received:");
            Console.WriteLine($"  Filter Type: {response.FilterType}");
            Console.WriteLine($"  Total Count: {response.TotalCount:N0}");
            Console.WriteLine($"  Filter Size: {response.FilterData.Length:N0} bytes");
            Console.WriteLine($"  Timestamp: {response.Timestamp}");
            Console.WriteLine($"  Metadata:");
            Console.WriteLine($"    Capacity: {response.Metadata.Capacity:N0}");
            Console.WriteLine($"    Bucket Size: {response.Metadata.BucketSize}");
            Console.WriteLine($"    Fingerprint Size: {response.Metadata.FingerprintSize} bytes");
            Console.WriteLine($"    False Positive Rate: {response.Metadata.FalsePositiveRate:E2}");

            // Deserialize filter
            _filter = CreateFilter(response.FilterType, response.Metadata);
            _filter.Deserialize(response.FilterData);
            _lastTimestamp = response.Timestamp;

            Console.WriteLine($"\nFilter deserialized successfully");
            Console.WriteLine($"Local filter count: {_filter.Count:N0}");

            // Start syncing deltas
            Console.WriteLine("\nStarting delta synchronization (every 10 seconds)...");
            _syncTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during initialization: {ex.Message}");
            throw;
        }
    }

    private async Task WaitForApiAsync()
    {
        var maxAttempts = 60;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/blacklist/status");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("API is ready!");
                    return;
                }
            }
            catch
            {
                // Ignore
            }

            attempt++;
            Console.WriteLine($"Waiting for API... (attempt {attempt}/{maxAttempts})");
            await Task.Delay(2000);
        }

        throw new Exception("API did not become ready in time");
    }

    private async void SyncDeltas(object? state)
    {
        try
        {
            var request = new DeltaRequest
            {
                LastTimestamp = _lastTimestamp,
                Count = 10
            };

            var response = await _httpClient.PostAsJsonAsync("/api/blacklist/deltas", request);
            var deltaResponse = await response.Content.ReadFromJsonAsync<DeltaResponse>();

            if (deltaResponse == null || deltaResponse.Deltas.Count == 0)
            {
                return;
            }

            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Received {deltaResponse.Deltas.Count} deltas");

            foreach (var delta in deltaResponse.Deltas)
            {
                ApplyDelta(delta);
                _lastTimestamp = Math.Max(_lastTimestamp, delta.Timestamp);
            }

            _totalDeltasReceived += deltaResponse.Deltas.Count;

            Console.WriteLine($"  Total deltas processed: {_totalDeltasReceived}");
            Console.WriteLine($"  Total added: {_totalAdded}, Total removed: {_totalRemoved}");
            Console.WriteLine($"  Current filter count: {_filter!.Count:N0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing deltas: {ex.Message}");
        }
    }

    private void ApplyDelta(BlacklistDelta delta)
    {
        foreach (var pan in delta.AddedPans)
        {
            _filter!.Add(pan);
            _totalAdded++;
        }

        foreach (var pan in delta.RemovedPans)
        {
            _filter!.Remove(pan);
            _totalRemoved++;
        }
    }

    public async Task<ValidationResponse> ValidateAsync(List<string> testPans, List<bool> expectedResults)
    {
        if (_filter == null)
        {
            throw new InvalidOperationException("Client not initialized");
        }

        Console.WriteLine($"\nValidating {testPans.Count} PANs...");

        // Validate locally
        int localCorrect = 0;
        int localFP = 0;
        int localFN = 0;

        for (int i = 0; i < testPans.Count; i++)
        {
            var actual = _filter.Contains(testPans[i]);
            var expected = expectedResults[i];

            if (actual == expected)
                localCorrect++;
            else if (actual && !expected)
                localFP++;
            else
                localFN++;
        }

        Console.WriteLine($"\nLocal validation:");
        Console.WriteLine($"  Correct: {localCorrect}/{testPans.Count}");
        Console.WriteLine($"  False Positives: {localFP}");
        Console.WriteLine($"  False Negatives: {localFN}");
        Console.WriteLine($"  Accuracy: {(double)localCorrect / testPans.Count:P2}");

        // Validate against server
        var request = new ValidationRequest
        {
            TestPans = testPans,
            ExpectedResults = expectedResults
        };

        var response = await _httpClient.PostAsJsonAsync("/api/blacklist/validate", request);
        var serverValidation = await response.Content.ReadFromJsonAsync<ValidationResponse>();

        if (serverValidation != null)
        {
            Console.WriteLine($"\nServer validation:");
            Console.WriteLine($"  Correct: {serverValidation.CorrectMatches}/{serverValidation.TotalTests}");
            Console.WriteLine($"  False Positives: {serverValidation.FalsePositives}");
            Console.WriteLine($"  False Negatives: {serverValidation.FalseNegatives}");
            Console.WriteLine($"  Accuracy: {serverValidation.Accuracy:P2}");
        }

        return new ValidationResponse
        {
            TotalTests = testPans.Count,
            CorrectMatches = localCorrect,
            FalsePositives = localFP,
            FalseNegatives = localFN,
            Accuracy = (double)localCorrect / testPans.Count,
            FalsePositiveRate = (double)localFP / testPans.Count
        };
    }

    private IBlacklistFilter CreateFilter(FilterType filterType, FilterMetadata metadata)
    {
        return filterType switch
        {
            FilterType.CuckooFilter => new CuckooFilter(
                metadata.Capacity,
                metadata.BucketSize,
                metadata.FingerprintSize
            ),
            FilterType.QuotientFilter => new QuotientFilter(
                metadata.Capacity,
                metadata.FingerprintSize * 8
            ),
            FilterType.HashTableGolomb => new HashTableGolombFilter(
                metadata.Capacity,
                256
            ),
            _ => throw new ArgumentException($"Unknown filter type: {filterType}")
        };
    }

    public IBlacklistFilter? GetFilter() => _filter;
}
