using System.Diagnostics;
using EMVBlacklist.Shared.Filters;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Benchmarks;

public class FilterComparison
{
    private const int InitialSize = 1_000_000;
    private const int TestSize = 10_000;

    public async Task RunComparison()
    {
        Console.WriteLine("Filter Comparison Report");
        Console.WriteLine($"Initial dataset: {InitialSize:N0} PANs");
        Console.WriteLine($"Test dataset: {TestSize:N0} PANs");
        Console.WriteLine("===========================================\n");

        // Test each filter type
        await TestFilter(FilterType.CuckooFilter);
        Console.WriteLine();
        await TestFilter(FilterType.QuotientFilter);
        Console.WriteLine();
        await TestFilter(FilterType.HashTableGolomb);
        Console.WriteLine();
    }

    private async Task TestFilter(FilterType filterType)
    {
        Console.WriteLine($"Testing {filterType}");
        Console.WriteLine("-------------------------------------------");

        // Create filter
        var filter = CreateFilter(filterType);
        var random = new Random(42);

        // Generate test data
        var testPans = new List<string>();
        for (int i = 0; i < InitialSize; i++)
        {
            testPans.Add(GeneratePAN(random));
        }

        // Measure insertion time
        var sw = Stopwatch.StartNew();
        foreach (var pan in testPans)
        {
            filter.Add(pan);
        }
        sw.Stop();
        var insertionTime = sw.Elapsed;

        Console.WriteLine($"Insertion Time: {insertionTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Throughput: {InitialSize / insertionTime.TotalSeconds:N0} ops/sec");

        // Measure serialization
        sw.Restart();
        var serialized = filter.Serialize();
        sw.Stop();
        var serializationTime = sw.Elapsed;

        Console.WriteLine($"\nSerialization:");
        Console.WriteLine($"  Time: {serializationTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Size: {serialized.Length:N0} bytes ({serialized.Length / 1024.0 / 1024.0:F2} MB)");
        Console.WriteLine($"  Compression ratio: {(double)serialized.Length / InitialSize:F2} bytes/item");

        // Measure deserialization
        var newFilter = CreateFilter(filterType);
        sw.Restart();
        newFilter.Deserialize(serialized);
        sw.Stop();
        var deserializationTime = sw.Elapsed;

        Console.WriteLine($"\nDeserialization:");
        Console.WriteLine($"  Time: {deserializationTime.TotalMilliseconds:F2} ms");

        // Measure lookup time
        sw.Restart();
        int found = 0;
        foreach (var pan in testPans.Take(TestSize))
        {
            if (filter.Contains(pan))
                found++;
        }
        sw.Stop();
        var lookupTime = sw.Elapsed;

        Console.WriteLine($"\nLookup Performance:");
        Console.WriteLine($"  Time: {lookupTime.TotalMilliseconds:F2} ms for {TestSize:N0} lookups");
        Console.WriteLine($"  Throughput: {TestSize / lookupTime.TotalSeconds:N0} ops/sec");
        Console.WriteLine($"  Found: {found:N0}/{TestSize:N0}");

        // Test false positives
        var nonExistentPans = new List<string>();
        for (int i = 0; i < TestSize; i++)
        {
            nonExistentPans.Add(GeneratePAN(new Random(i + 1000000)));
        }

        int falsePositives = 0;
        foreach (var pan in nonExistentPans)
        {
            if (filter.Contains(pan))
                falsePositives++;
        }

        Console.WriteLine($"\nFalse Positive Analysis:");
        Console.WriteLine($"  Tests: {TestSize:N0}");
        Console.WriteLine($"  False Positives: {falsePositives}");
        Console.WriteLine($"  False Positive Rate: {(double)falsePositives / TestSize:P4}");

        // Test delta operations
        sw.Restart();
        for (int i = 0; i < 100; i++)
        {
            var newPan = GeneratePAN(new Random(i + 2000000));
            filter.Add(newPan);
        }
        sw.Stop();
        var deltaAddTime = sw.Elapsed;

        sw.Restart();
        foreach (var pan in testPans.Take(100))
        {
            filter.Remove(pan);
        }
        sw.Stop();
        var deltaRemoveTime = sw.Elapsed;

        Console.WriteLine($"\nDelta Operations:");
        Console.WriteLine($"  Add 100 items: {deltaAddTime.TotalMilliseconds:F2} ms ({100 / deltaAddTime.TotalSeconds:F0} ops/sec)");
        Console.WriteLine($"  Remove 100 items: {deltaRemoveTime.TotalMilliseconds:F2} ms ({100 / deltaRemoveTime.TotalSeconds:F0} ops/sec)");

        var metadata = filter.GetMetadata();
        Console.WriteLine($"\nMetadata:");
        Console.WriteLine($"  Capacity: {metadata.Capacity:N0}");
        Console.WriteLine($"  Bucket Size: {metadata.BucketSize}");
        Console.WriteLine($"  Fingerprint Size: {metadata.FingerprintSize} bytes");
        Console.WriteLine($"  Theoretical FP Rate: {metadata.FalsePositiveRate:E2}");
    }

    private IBlacklistFilter CreateFilter(FilterType filterType)
    {
        return filterType switch
        {
            FilterType.CuckooFilter => new CuckooFilter(InitialSize + 100000, 4, 2),
            FilterType.QuotientFilter => new QuotientFilter(InitialSize + 100000, 16),
            FilterType.HashTableGolomb => new HashTableGolombFilter(InitialSize + 100000, 256),
            _ => throw new ArgumentException($"Unknown filter type: {filterType}")
        };
    }

    private string GeneratePAN(Random random)
    {
        var pan = "";
        for (int i = 0; i < 16; i++)
        {
            pan += random.Next(0, 10).ToString();
        }
        return pan;
    }
}
