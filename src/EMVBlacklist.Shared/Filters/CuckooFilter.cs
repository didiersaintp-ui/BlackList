using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Shared.Filters;

public class CuckooFilter : IBlacklistFilter
{
    private const int MaxKicks = 500;
    private readonly int _bucketSize;
    private readonly int _fingerprintSize;
    private readonly int _numBuckets;
    private readonly Bucket[] _buckets;
    private int _count;

    public FilterType FilterType => FilterType.CuckooFilter;
    public int Count => _count;

    private class Bucket
    {
        public byte[][] Fingerprints { get; }

        public Bucket(int size, int fingerprintSize)
        {
            Fingerprints = new byte[size][];
            for (int i = 0; i < size; i++)
            {
                Fingerprints[i] = new byte[fingerprintSize];
            }
        }
    }

    public CuckooFilter(int capacity = 1000000, int bucketSize = 4, int fingerprintSize = 2)
    {
        _bucketSize = bucketSize;
        _fingerprintSize = fingerprintSize;
        _numBuckets = (int)Math.Ceiling((double)capacity / bucketSize);
        _buckets = new Bucket[_numBuckets];

        for (int i = 0; i < _numBuckets; i++)
        {
            _buckets[i] = new Bucket(bucketSize, fingerprintSize);
        }
    }

    public void Add(string pan)
    {
        var (index1, fingerprint) = Hash(pan);
        var index2 = AlternateIndex(index1, fingerprint);

        // Try to insert in bucket1
        if (TryInsert(_buckets[index1], fingerprint))
        {
            _count++;
            return;
        }

        // Try to insert in bucket2
        if (TryInsert(_buckets[index2], fingerprint))
        {
            _count++;
            return;
        }

        // Randomly pick one and relocate
        var currentIndex = Random.Shared.Next(2) == 0 ? index1 : index2;
        for (int kicks = 0; kicks < MaxKicks; kicks++)
        {
            var slot = Random.Shared.Next(_bucketSize);
            var temp = _buckets[currentIndex].Fingerprints[slot];
            Array.Copy(fingerprint, _buckets[currentIndex].Fingerprints[slot], _fingerprintSize);
            fingerprint = temp;

            currentIndex = AlternateIndex(currentIndex, fingerprint);
            if (TryInsert(_buckets[currentIndex], fingerprint))
            {
                _count++;
                return;
            }
        }

        // Filter is full or needs expansion
        _count++;
    }

    public bool Contains(string pan)
    {
        var (index1, fingerprint) = Hash(pan);
        var index2 = AlternateIndex(index1, fingerprint);

        return FindFingerprint(_buckets[index1], fingerprint) ||
               FindFingerprint(_buckets[index2], fingerprint);
    }

    public void Remove(string pan)
    {
        var (index1, fingerprint) = Hash(pan);
        var index2 = AlternateIndex(index1, fingerprint);

        if (RemoveFingerprint(_buckets[index1], fingerprint))
        {
            _count--;
            return;
        }

        if (RemoveFingerprint(_buckets[index2], fingerprint))
        {
            _count--;
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(_bucketSize);
        writer.Write(_fingerprintSize);
        writer.Write(_numBuckets);
        writer.Write(_count);

        foreach (var bucket in _buckets)
        {
            foreach (var fingerprint in bucket.Fingerprints)
            {
                writer.Write(fingerprint);
            }
        }

        return ms.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        var bucketSize = reader.ReadInt32();
        var fingerprintSize = reader.ReadInt32();
        var numBuckets = reader.ReadInt32();
        _count = reader.ReadInt32();

        if (bucketSize != _bucketSize || fingerprintSize != _fingerprintSize || numBuckets != _numBuckets)
        {
            throw new InvalidOperationException("Filter configuration mismatch");
        }

        for (int i = 0; i < _numBuckets; i++)
        {
            for (int j = 0; j < _bucketSize; j++)
            {
                _buckets[i].Fingerprints[j] = reader.ReadBytes(_fingerprintSize);
            }
        }
    }

    public FilterMetadata GetMetadata()
    {
        return new FilterMetadata
        {
            Capacity = _numBuckets * _bucketSize,
            BucketSize = _bucketSize,
            FingerprintSize = _fingerprintSize,
            FalsePositiveRate = CalculateFalsePositiveRate()
        };
    }

    private (int index, byte[] fingerprint) Hash(string pan)
    {
        var hashBytes = XxHash64.Hash(Encoding.UTF8.GetBytes(pan));
        var hash = BitConverter.ToUInt64(hashBytes, 0);

        var index = (int)(hash % (uint)_numBuckets);
        var fingerprint = new byte[_fingerprintSize];

        for (int i = 0; i < _fingerprintSize; i++)
        {
            fingerprint[i] = hashBytes[i % 8];
        }

        // Ensure fingerprint is not all zeros
        if (fingerprint.All(b => b == 0))
        {
            fingerprint[0] = 1;
        }

        return (index, fingerprint);
    }

    private int AlternateIndex(int index, byte[] fingerprint)
    {
        var fpHashBytes = XxHash32.Hash(fingerprint);
        var fpHash = BitConverter.ToUInt32(fpHashBytes, 0);
        return (int)((index ^ fpHash) % (uint)_numBuckets);
    }

    private bool TryInsert(Bucket bucket, byte[] fingerprint)
    {
        for (int i = 0; i < _bucketSize; i++)
        {
            if (bucket.Fingerprints[i].All(b => b == 0))
            {
                Array.Copy(fingerprint, bucket.Fingerprints[i], _fingerprintSize);
                return true;
            }
        }
        return false;
    }

    private bool FindFingerprint(Bucket bucket, byte[] fingerprint)
    {
        for (int i = 0; i < _bucketSize; i++)
        {
            if (bucket.Fingerprints[i].SequenceEqual(fingerprint))
            {
                return true;
            }
        }
        return false;
    }

    private bool RemoveFingerprint(Bucket bucket, byte[] fingerprint)
    {
        for (int i = 0; i < _bucketSize; i++)
        {
            if (bucket.Fingerprints[i].SequenceEqual(fingerprint))
            {
                Array.Clear(bucket.Fingerprints[i]);
                return true;
            }
        }
        return false;
    }

    private double CalculateFalsePositiveRate()
    {
        var bitsPerFingerprint = _fingerprintSize * 8;
        return Math.Pow(2, -bitsPerFingerprint) * _bucketSize * 2;
    }
}
