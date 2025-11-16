namespace EMVBlacklist.Shared.Models;

public class BlacklistDelta
{
    public long Timestamp { get; set; }
    public List<string> AddedPans { get; set; } = new();
    public List<string> RemovedPans { get; set; } = new();
}

public class DeltaRequest
{
    public long LastTimestamp { get; set; }
    public int Count { get; set; } = 10;
}

public class InitialLoadResponse
{
    public byte[] FilterData { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public int TotalCount { get; set; }
    public FilterType FilterType { get; set; }
    public FilterMetadata Metadata { get; set; } = new();
}

public class DeltaResponse
{
    public List<BlacklistDelta> Deltas { get; set; } = new();
    public long CurrentTimestamp { get; set; }
}

public class FilterMetadata
{
    public int Capacity { get; set; }
    public int BucketSize { get; set; }
    public int FingerprintSize { get; set; }
    public double FalsePositiveRate { get; set; }
}

public class ValidationRequest
{
    public List<string> TestPans { get; set; } = new();
    public List<bool> ExpectedResults { get; set; } = new();
}

public class ValidationResponse
{
    public int TotalTests { get; set; }
    public int CorrectMatches { get; set; }
    public int FalsePositives { get; set; }
    public int FalseNegatives { get; set; }
    public double Accuracy { get; set; }
    public double FalsePositiveRate { get; set; }
}
