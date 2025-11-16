using System.Collections.Concurrent;
using EMVBlacklist.Shared.Filters;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.API.Services;

public class BlacklistService
{
    private IBlacklistFilter _filter;
    private readonly ConcurrentQueue<BlacklistDelta> _deltaQueue;
    private readonly HashSet<string> _actualBlacklist;
    private long _currentTimestamp;
    private readonly object _lock = new();
    private readonly Timer _deltaGeneratorTimer;

    public BlacklistService(FilterType filterType)
    {
        _filter = CreateFilter(filterType);
        _deltaQueue = new ConcurrentQueue<BlacklistDelta>();
        _actualBlacklist = new HashSet<string>();
        _currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Start generating deltas every second
        _deltaGeneratorTimer = new Timer(GenerateDelta, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void InitializeWithData(int count = 1_000_000)
    {
        lock (_lock)
        {
            Console.WriteLine($"Initializing blacklist with {count} PANs using {_filter.FilterType}...");
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < count; i++)
            {
                var pan = GeneratePAN(i);
                _filter.Add(pan);
                _actualBlacklist.Add(pan);

                if ((i + 1) % 100000 == 0)
                {
                    Console.WriteLine($"Added {i + 1:N0} PANs...");
                }
            }

            var elapsed = DateTime.UtcNow - startTime;
            Console.WriteLine($"Initialization complete in {elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"Filter size: {_filter.Count:N0} items");
        }
    }

    public InitialLoadResponse GetInitialLoad()
    {
        lock (_lock)
        {
            var filterData = _filter.Serialize();
            var metadata = _filter.GetMetadata();

            Console.WriteLine($"Initial load - Filter data size: {filterData.Length:N0} bytes");

            return new InitialLoadResponse
            {
                FilterData = filterData,
                Timestamp = _currentTimestamp,
                TotalCount = _filter.Count,
                FilterType = _filter.FilterType,
                Metadata = metadata
            };
        }
    }

    public DeltaResponse GetDeltas(long lastTimestamp, int count = 10)
    {
        var deltas = _deltaQueue
            .Where(d => d.Timestamp > lastTimestamp)
            .OrderBy(d => d.Timestamp)
            .Take(count)
            .ToList();

        return new DeltaResponse
        {
            Deltas = deltas,
            CurrentTimestamp = _currentTimestamp
        };
    }

    public ValidationResponse Validate(ValidationRequest request)
    {
        lock (_lock)
        {
            int correctMatches = 0;
            int falsePositives = 0;
            int falseNegatives = 0;

            for (int i = 0; i < request.TestPans.Count; i++)
            {
                var pan = request.TestPans[i];
                var expected = request.ExpectedResults[i];
                var actual = _filter.Contains(pan);

                if (actual == expected)
                {
                    correctMatches++;
                }
                else if (actual && !expected)
                {
                    falsePositives++;
                }
                else if (!actual && expected)
                {
                    falseNegatives++;
                }
            }

            var totalTests = request.TestPans.Count;
            var accuracy = (double)correctMatches / totalTests;
            var fpRate = (double)falsePositives / totalTests;

            return new ValidationResponse
            {
                TotalTests = totalTests,
                CorrectMatches = correctMatches,
                FalsePositives = falsePositives,
                FalseNegatives = falseNegatives,
                Accuracy = accuracy,
                FalsePositiveRate = fpRate
            };
        }
    }

    public void SwitchFilter(FilterType newFilterType)
    {
        lock (_lock)
        {
            Console.WriteLine($"Switching filter from {_filter.FilterType} to {newFilterType}...");
            var startTime = DateTime.UtcNow;

            var newFilter = CreateFilter(newFilterType);

            // Copy all items to new filter
            foreach (var pan in _actualBlacklist)
            {
                newFilter.Add(pan);
            }

            _filter = newFilter;

            var elapsed = DateTime.UtcNow - startTime;
            Console.WriteLine($"Filter switch complete in {elapsed.TotalSeconds:F2} seconds");
        }
    }

    private void GenerateDelta(object? state)
    {
        lock (_lock)
        {
            _currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var delta = new BlacklistDelta
            {
                Timestamp = _currentTimestamp
            };

            // Randomly add or remove a PAN
            if (Random.Shared.Next(2) == 0 && _actualBlacklist.Count > 100000)
            {
                // Remove a random PAN
                var panToRemove = _actualBlacklist.ElementAt(Random.Shared.Next(_actualBlacklist.Count));
                _filter.Remove(panToRemove);
                _actualBlacklist.Remove(panToRemove);
                delta.RemovedPans.Add(panToRemove);
            }
            else
            {
                // Add a new PAN
                var newPan = GeneratePAN(_actualBlacklist.Count + Random.Shared.Next(1000000, 9999999));
                _filter.Add(newPan);
                _actualBlacklist.Add(newPan);
                delta.AddedPans.Add(newPan);
            }

            _deltaQueue.Enqueue(delta);

            // Keep only last 100 deltas in memory
            while (_deltaQueue.Count > 100)
            {
                _deltaQueue.TryDequeue(out _);
            }
        }
    }

    private string GeneratePAN(int seed)
    {
        // Generate a realistic-looking PAN (16 digits)
        var random = new Random(seed);
        var pan = "";
        for (int i = 0; i < 16; i++)
        {
            pan += random.Next(0, 10).ToString();
        }
        return pan;
    }

    private IBlacklistFilter CreateFilter(FilterType filterType)
    {
        return filterType switch
        {
            FilterType.CuckooFilter => new CuckooFilter(1_200_000, 4, 2),
            FilterType.QuotientFilter => new QuotientFilter(1_200_000, 16),
            FilterType.HashTableGolomb => new HashTableGolombFilter(1_200_000, 256),
            _ => throw new ArgumentException($"Unknown filter type: {filterType}")
        };
    }

    public IBlacklistFilter GetFilter() => _filter;

    public HashSet<string> GetActualBlacklist() => _actualBlacklist;
}
