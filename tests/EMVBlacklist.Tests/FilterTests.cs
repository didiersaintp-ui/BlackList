using Xunit;
using EMVBlacklist.Shared.Filters;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Tests;

public class FilterTests
{
    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_AddAndContains_ShouldWork(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var pan = "1234567890123456";

        // Act
        filter.Add(pan);

        // Assert
        Assert.True(filter.Contains(pan));
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_Remove_ShouldWork(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var pan = "1234567890123456";
        filter.Add(pan);

        // Act
        filter.Remove(pan);

        // Assert
        Assert.False(filter.Contains(pan));
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_SerializeDeserialize_ShouldPreserveData(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var testPans = new List<string>
        {
            "1234567890123456",
            "9876543210987654",
            "1111222233334444"
        };

        foreach (var pan in testPans)
        {
            filter.Add(pan);
        }

        // Act
        var serialized = filter.Serialize();
        var newFilter = CreateFilter(filterType);
        newFilter.Deserialize(serialized);

        // Assert
        foreach (var pan in testPans)
        {
            Assert.True(newFilter.Contains(pan), $"PAN {pan} should be in filter after deserialization");
        }
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_Count_ShouldBeAccurate(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var count = 100;

        // Act
        for (int i = 0; i < count; i++)
        {
            filter.Add($"PAN{i:D16}");
        }

        // Assert
        Assert.Equal(count, filter.Count);
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_LargeDataset_ShouldWork(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var count = 10000;
        var testPans = new List<string>();

        for (int i = 0; i < count; i++)
        {
            testPans.Add($"PAN{i:D16}");
        }

        // Act
        foreach (var pan in testPans)
        {
            filter.Add(pan);
        }

        // Assert
        var found = 0;
        foreach (var pan in testPans)
        {
            if (filter.Contains(pan))
                found++;
        }

        Assert.True(found >= count * 0.99, $"Should find at least 99% of items, found {found}/{count}");
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_FalsePositiveRate_ShouldBeLow(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);
        var insertCount = 1000;
        var testCount = 1000;

        // Insert items
        for (int i = 0; i < insertCount; i++)
        {
            filter.Add($"INSERT{i:D16}");
        }

        // Test with non-existent items
        var falsePositives = 0;
        for (int i = 0; i < testCount; i++)
        {
            if (filter.Contains($"NOTEXIST{i:D16}"))
            {
                falsePositives++;
            }
        }

        var fpRate = (double)falsePositives / testCount;

        // Assert - allow up to 5% false positive rate for this test
        Assert.True(fpRate < 0.05, $"False positive rate {fpRate:P2} is too high");
    }

    [Theory]
    [InlineData(FilterType.CuckooFilter)]
    [InlineData(FilterType.QuotientFilter)]
    [InlineData(FilterType.HashTableGolomb)]
    public void Filter_GetMetadata_ShouldReturnValidData(FilterType filterType)
    {
        // Arrange
        var filter = CreateFilter(filterType);

        // Act
        var metadata = filter.GetMetadata();

        // Assert
        Assert.True(metadata.Capacity > 0);
        Assert.True(metadata.BucketSize > 0);
        Assert.True(metadata.FingerprintSize > 0);
        Assert.True(metadata.FalsePositiveRate >= 0);
    }

    private IBlacklistFilter CreateFilter(FilterType filterType)
    {
        return filterType switch
        {
            FilterType.CuckooFilter => new CuckooFilter(20000, 4, 2),
            FilterType.QuotientFilter => new QuotientFilter(20000, 16),
            FilterType.HashTableGolomb => new HashTableGolombFilter(20000, 256),
            _ => throw new ArgumentException($"Unknown filter type: {filterType}")
        };
    }
}
