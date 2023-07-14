using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace StabilityMatrix.Core.Helper.Cache;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LRUCache<TK,TV> where TK : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TK, LinkedListNode<LRUCacheItem<TK, TV>>> cacheMap = new();
    private readonly LinkedList<LRUCacheItem<TK, TV>> lruList = new();

    public LRUCache(int capacity)
    {
        this.capacity = capacity;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public TV? Get(TK key)
    {
        if (cacheMap.TryGetValue(key, out var node))
        {
            var value = node.Value.Value;
            lruList.Remove(node);
            lruList.AddLast(node);
            return value;
        }
        return default;
    }
    
    public bool Get(TK key, out TV? value)
    {
        value = Get(key);
        return value != null;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Add(TK key, TV val)
    {
        if (cacheMap.TryGetValue(key, out var existingNode))
        {
            lruList.Remove(existingNode);
        }
        else if (cacheMap.Count >= capacity)
        {
            RemoveFirst();
        }

        var cacheItem = new LRUCacheItem<TK, TV>(key, val);
        var node = new LinkedListNode<LRUCacheItem<TK, TV>>(cacheItem);
        lruList.AddLast(node);
        cacheMap[key] = node;
    }

    private void RemoveFirst()
    {
        // Remove from LRUPriority
        var node = lruList.First;
        lruList.RemoveFirst();
        
        if (node == null) return;
        
        // Remove from cache
        cacheMap.Remove(node.Value.Key);
    }
}

// ReSharper disable once InconsistentNaming
internal class LRUCacheItem<TK,TV>
{
    public LRUCacheItem(TK k, TV v)
    {
        Key = k;
        Value = v;
    }
    public TK Key;
    public TV Value;
}
