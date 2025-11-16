using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.Shared.Interfaces;

public interface IBlacklistFilter
{
    FilterType FilterType { get; }

    void Add(string pan);
    bool Contains(string pan);
    void Remove(string pan);
    byte[] Serialize();
    void Deserialize(byte[] data);
    int Count { get; }
    FilterMetadata GetMetadata();
}
