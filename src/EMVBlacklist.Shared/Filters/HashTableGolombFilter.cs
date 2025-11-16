using System.IO.Hashing;
using System.Text;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Shared.Filters;

public class HashTableGolombFilter : IBlacklistFilter
{
    private readonly HashSet<ulong> _hashTable;
    private readonly int _golombParameter;
    private List<ulong> _sortedHashes;
    private byte[] _golombEncoded;

    public FilterType FilterType => FilterType.HashTableGolomb;
    public int Count => _hashTable.Count;

    public HashTableGolombFilter(int capacity = 1000000, int golombParameter = 256)
    {
        _hashTable = new HashSet<ulong>(capacity);
        _golombParameter = golombParameter;
        _sortedHashes = new List<ulong>();
        _golombEncoded = Array.Empty<byte>();
    }

    public void Add(string pan)
    {
        var hash = ComputeHash(pan);
        _hashTable.Add(hash);
    }

    public bool Contains(string pan)
    {
        var hash = ComputeHash(pan);
        return _hashTable.Contains(hash);
    }

    public void Remove(string pan)
    {
        var hash = ComputeHash(pan);
        _hashTable.Remove(hash);
    }

    public byte[] Serialize()
    {
        // Update Golomb encoding before serialization
        EncodeGolomb();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(_golombParameter);
        writer.Write(_hashTable.Count);

        // Write Golomb-encoded data
        writer.Write(_golombEncoded.Length);
        writer.Write(_golombEncoded);

        return ms.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        var golombParameter = reader.ReadInt32();
        var count = reader.ReadInt32();

        if (golombParameter != _golombParameter)
        {
            throw new InvalidOperationException("Filter configuration mismatch");
        }

        // Read Golomb-encoded data
        var encodedLength = reader.ReadInt32();
        _golombEncoded = reader.ReadBytes(encodedLength);

        // Decode into hash table
        DecodeGolomb(count);
    }

    public FilterMetadata GetMetadata()
    {
        return new FilterMetadata
        {
            Capacity = _hashTable.Count,
            BucketSize = 1,
            FingerprintSize = 8, // 64-bit hash
            FalsePositiveRate = 0.0 // Hash table has no false positives
        };
    }

    private ulong ComputeHash(string pan)
    {
        var hashBytes = XxHash64.Hash(Encoding.UTF8.GetBytes(pan));
        return BitConverter.ToUInt64(hashBytes, 0);
    }

    private void EncodeGolomb()
    {
        if (_hashTable.Count == 0)
        {
            _golombEncoded = Array.Empty<byte>();
            return;
        }

        // Sort hashes
        _sortedHashes = _hashTable.OrderBy(h => h).ToList();

        var bitWriter = new BitWriter();
        ulong previous = 0;

        foreach (var hash in _sortedHashes)
        {
            var delta = hash - previous;
            EncodeGolombValue(bitWriter, delta, _golombParameter);
            previous = hash;
        }

        _golombEncoded = bitWriter.ToArray();
    }

    private void DecodeGolomb(int count)
    {
        _hashTable.Clear();

        if (_golombEncoded.Length == 0 || count == 0)
        {
            return;
        }

        var bitReader = new BitReader(_golombEncoded);
        ulong current = 0;

        for (int i = 0; i < count; i++)
        {
            var delta = DecodeGolombValue(bitReader, _golombParameter);
            current += delta;
            _hashTable.Add(current);
        }
    }

    private void EncodeGolombValue(BitWriter writer, ulong value, int m)
    {
        var q = value / (ulong)m;
        var r = value % (ulong)m;

        // Unary encode quotient
        for (ulong i = 0; i < q; i++)
        {
            writer.WriteBit(1);
        }
        writer.WriteBit(0);

        // Binary encode remainder
        var b = (int)Math.Ceiling(Math.Log2(m));
        writer.WriteBits(r, b);
    }

    private ulong DecodeGolombValue(BitReader reader, int m)
    {
        // Decode unary quotient
        ulong q = 0;
        while (reader.ReadBit() == 1)
        {
            q++;
        }

        // Decode binary remainder
        var b = (int)Math.Ceiling(Math.Log2(m));
        var r = reader.ReadBits(b);

        return q * (ulong)m + r;
    }

    private class BitWriter
    {
        private readonly List<byte> _bytes = new();
        private byte _currentByte;
        private int _bitPosition;

        public void WriteBit(int bit)
        {
            if (bit != 0)
            {
                _currentByte |= (byte)(1 << (7 - _bitPosition));
            }

            _bitPosition++;
            if (_bitPosition == 8)
            {
                _bytes.Add(_currentByte);
                _currentByte = 0;
                _bitPosition = 0;
            }
        }

        public void WriteBits(ulong value, int numBits)
        {
            for (int i = numBits - 1; i >= 0; i--)
            {
                WriteBit((int)((value >> i) & 1));
            }
        }

        public byte[] ToArray()
        {
            if (_bitPosition > 0)
            {
                _bytes.Add(_currentByte);
            }
            return _bytes.ToArray();
        }
    }

    private class BitReader
    {
        private readonly byte[] _bytes;
        private int _bytePosition;
        private int _bitPosition;

        public BitReader(byte[] bytes)
        {
            _bytes = bytes;
        }

        public int ReadBit()
        {
            if (_bytePosition >= _bytes.Length)
            {
                return 0;
            }

            var bit = (_bytes[_bytePosition] >> (7 - _bitPosition)) & 1;
            _bitPosition++;

            if (_bitPosition == 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            return bit;
        }

        public ulong ReadBits(int numBits)
        {
            ulong value = 0;
            for (int i = 0; i < numBits; i++)
            {
                value = (value << 1) | (uint)ReadBit();
            }
            return value;
        }
    }
}
