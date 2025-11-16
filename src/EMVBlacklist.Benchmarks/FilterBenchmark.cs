using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EMVBlacklist.Shared.Filters;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class FilterBenchmark
{
    private List<string> _testPans = new();
    private List<string> _nonExistentPans = new();
    private const int DataSize = 100_000;
    private const int TestSize = 10_000;

    private IBlacklistFilter _cuckooFilter = null!;
    private IBlacklistFilter _quotientFilter = null!;
    private IBlacklistFilter _hashTableGolombFilter = null!;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("Setting up benchmark data...");

        // Generate test PANs
        var random = new Random(42);
        for (int i = 0; i < DataSize; i++)
        {
            _testPans.Add(GeneratePAN(random));
        }

        // Generate non-existent PANs
        for (int i = 0; i < TestSize; i++)
        {
            _nonExistentPans.Add(GeneratePAN(random));
        }

        // Initialize filters
        _cuckooFilter = new CuckooFilter(DataSize + 10000, 4, 2);
        _quotientFilter = new QuotientFilter(DataSize + 10000, 16);
        _hashTableGolombFilter = new HashTableGolombFilter(DataSize + 10000, 256);

        // Populate filters
        foreach (var pan in _testPans)
        {
            _cuckooFilter.Add(pan);
            _quotientFilter.Add(pan);
            _hashTableGolombFilter.Add(pan);
        }

        Console.WriteLine("Setup complete.");
    }

    [Benchmark]
    public void CuckooFilter_Insert()
    {
        var filter = new CuckooFilter(DataSize + 10000, 4, 2);
        foreach (var pan in _testPans)
        {
            filter.Add(pan);
        }
    }

    [Benchmark]
    public void QuotientFilter_Insert()
    {
        var filter = new QuotientFilter(DataSize + 10000, 16);
        foreach (var pan in _testPans)
        {
            filter.Add(pan);
        }
    }

    [Benchmark]
    public void HashTableGolomb_Insert()
    {
        var filter = new HashTableGolombFilter(DataSize + 10000, 256);
        foreach (var pan in _testPans)
        {
            filter.Add(pan);
        }
    }

    [Benchmark]
    public int CuckooFilter_Lookup()
    {
        int count = 0;
        foreach (var pan in _testPans.Take(1000))
        {
            if (_cuckooFilter.Contains(pan))
                count++;
        }
        return count;
    }

    [Benchmark]
    public int QuotientFilter_Lookup()
    {
        int count = 0;
        foreach (var pan in _testPans.Take(1000))
        {
            if (_quotientFilter.Contains(pan))
                count++;
        }
        return count;
    }

    [Benchmark]
    public int HashTableGolomb_Lookup()
    {
        int count = 0;
        foreach (var pan in _testPans.Take(1000))
        {
            if (_hashTableGolombFilter.Contains(pan))
                count++;
        }
        return count;
    }

    [Benchmark]
    public byte[] CuckooFilter_Serialize()
    {
        return _cuckooFilter.Serialize();
    }

    [Benchmark]
    public byte[] QuotientFilter_Serialize()
    {
        return _quotientFilter.Serialize();
    }

    [Benchmark]
    public byte[] HashTableGolomb_Serialize()
    {
        return _hashTableGolombFilter.Serialize();
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
