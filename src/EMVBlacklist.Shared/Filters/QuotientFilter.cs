using System.IO.Hashing;
using System.Text;
using EMVBlacklist.Shared.Interfaces;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Shared.Filters;

public class QuotientFilter : IBlacklistFilter
{
    private readonly int _quotientBits;
    private readonly int _remainderBits;
    private readonly int _numSlots;
    private readonly Slot[] _slots;
    private int _count;

    public FilterType FilterType => FilterType.QuotientFilter;
    public int Count => _count;

    private class Slot
    {
        public uint Remainder { get; set; }
        public bool IsOccupied { get; set; }
        public bool IsContinuation { get; set; }
        public bool IsShifted { get; set; }
    }

    public QuotientFilter(int capacity = 1000000, int remainderBits = 16)
    {
        _remainderBits = remainderBits;
        _quotientBits = (int)Math.Ceiling(Math.Log2(capacity));
        _numSlots = 1 << _quotientBits;
        _slots = new Slot[_numSlots];

        for (int i = 0; i < _numSlots; i++)
        {
            _slots[i] = new Slot();
        }
    }

    public void Add(string pan)
    {
        var (quotient, remainder) = Hash(pan);
        Insert(quotient, remainder);
        _count++;
    }

    public bool Contains(string pan)
    {
        var (quotient, remainder) = Hash(pan);
        return Lookup(quotient, remainder);
    }

    public void Remove(string pan)
    {
        var (quotient, remainder) = Hash(pan);
        if (Delete(quotient, remainder))
        {
            _count--;
        }
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(_quotientBits);
        writer.Write(_remainderBits);
        writer.Write(_numSlots);
        writer.Write(_count);

        foreach (var slot in _slots)
        {
            writer.Write(slot.Remainder);
            writer.Write(slot.IsOccupied);
            writer.Write(slot.IsContinuation);
            writer.Write(slot.IsShifted);
        }

        return ms.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        var quotientBits = reader.ReadInt32();
        var remainderBits = reader.ReadInt32();
        var numSlots = reader.ReadInt32();
        _count = reader.ReadInt32();

        if (quotientBits != _quotientBits || remainderBits != _remainderBits || numSlots != _numSlots)
        {
            throw new InvalidOperationException("Filter configuration mismatch");
        }

        for (int i = 0; i < _numSlots; i++)
        {
            _slots[i].Remainder = reader.ReadUInt32();
            _slots[i].IsOccupied = reader.ReadBoolean();
            _slots[i].IsContinuation = reader.ReadBoolean();
            _slots[i].IsShifted = reader.ReadBoolean();
        }
    }

    public FilterMetadata GetMetadata()
    {
        return new FilterMetadata
        {
            Capacity = _numSlots,
            BucketSize = 1,
            FingerprintSize = _remainderBits / 8,
            FalsePositiveRate = Math.Pow(2, -_remainderBits)
        };
    }

    private (int quotient, uint remainder) Hash(string pan)
    {
        var hashBytes = XxHash64.Hash(Encoding.UTF8.GetBytes(pan));
        var hash = BitConverter.ToUInt64(hashBytes, 0);
        var quotient = (int)(hash >> _remainderBits) & ((_numSlots - 1));
        var remainder = (uint)(hash & ((1UL << _remainderBits) - 1));

        // Ensure remainder is not zero
        if (remainder == 0)
        {
            remainder = 1;
        }

        return (quotient, remainder);
    }

    private void Insert(int quotient, uint remainder)
    {
        int current = quotient;

        // Find the run for this quotient
        if (!_slots[quotient].IsOccupied)
        {
            // Simple case: slot is empty
            _slots[quotient].Remainder = remainder;
            _slots[quotient].IsOccupied = true;
            return;
        }

        // Find insertion point
        int runStart = FindRunStart(quotient);
        int insertPos = FindInsertPosition(runStart, remainder);

        // Shift elements to make room
        ShiftRight(insertPos);

        // Insert the remainder
        _slots[insertPos].Remainder = remainder;
        _slots[insertPos].IsContinuation = (insertPos != quotient);
        _slots[insertPos].IsShifted = false;
        _slots[quotient].IsOccupied = true;
    }

    private bool Lookup(int quotient, uint remainder)
    {
        if (!_slots[quotient].IsOccupied)
        {
            return false;
        }

        int runStart = FindRunStart(quotient);
        int current = runStart;

        do
        {
            if (_slots[current].Remainder == remainder)
            {
                return true;
            }

            current = (current + 1) % _numSlots;
        }
        while (_slots[current].IsContinuation);

        return false;
    }

    private bool Delete(int quotient, uint remainder)
    {
        if (!_slots[quotient].IsOccupied)
        {
            return false;
        }

        int runStart = FindRunStart(quotient);
        int current = runStart;

        do
        {
            if (_slots[current].Remainder == remainder)
            {
                ShiftLeft(current);
                return true;
            }

            current = (current + 1) % _numSlots;
        }
        while (_slots[current].IsContinuation);

        return false;
    }

    private int FindRunStart(int quotient)
    {
        int start = quotient;

        // Count runs before this quotient
        int runCount = 0;
        for (int i = 0; i < quotient; i++)
        {
            if (_slots[i].IsOccupied)
            {
                runCount++;
            }
            if (_slots[i].IsShifted || _slots[i].IsContinuation)
            {
                runCount--;
            }
        }

        // Find the actual start position
        while (runCount > 0)
        {
            start = (start + 1) % _numSlots;
            if (!_slots[start].IsContinuation)
            {
                runCount--;
            }
        }

        return start;
    }

    private int FindInsertPosition(int runStart, uint remainder)
    {
        int pos = runStart;

        while (_slots[pos].IsContinuation && _slots[pos].Remainder < remainder)
        {
            pos = (pos + 1) % _numSlots;
        }

        return pos;
    }

    private void ShiftRight(int position)
    {
        int current = _numSlots - 1;
        while (current > position)
        {
            if (_slots[current - 1].IsOccupied || _slots[current - 1].IsContinuation)
            {
                _slots[current] = new Slot
                {
                    Remainder = _slots[current - 1].Remainder,
                    IsOccupied = _slots[current - 1].IsOccupied,
                    IsContinuation = _slots[current - 1].IsContinuation,
                    IsShifted = true
                };
            }
            current--;
        }
    }

    private void ShiftLeft(int position)
    {
        for (int i = position; i < _numSlots - 1; i++)
        {
            _slots[i] = new Slot
            {
                Remainder = _slots[i + 1].Remainder,
                IsOccupied = _slots[i + 1].IsOccupied,
                IsContinuation = _slots[i + 1].IsContinuation,
                IsShifted = _slots[i + 1].IsShifted
            };
        }

        _slots[_numSlots - 1] = new Slot();
    }
}
