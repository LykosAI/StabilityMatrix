// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Generic in-memory storage of items
/// </summary>
/// <typeparam name="T">T defines the type of item stored</typeparam>
public class InMemoryStorage<T>
{
    private readonly Dictionary<string, LinkedListNode<InMemoryStorageItem<T>>> _inMemoryStorage = new();
    private readonly LinkedList<InMemoryStorageItem<T>> _lruList = [];

    private int _maxItemCount;
    private readonly Lock _settingMaxItemCountLocker = new();

    /// <summary>
    /// Gets or sets the maximum count of Items that can be stored in this InMemoryStorage instance.
    /// </summary>
    public int MaxItemCount
    {
        get => _maxItemCount;
        set
        {
            if (_maxItemCount == value)
            {
                return;
            }

            _maxItemCount = value;

            lock (_settingMaxItemCountLocker)
            {
                EnsureStorageBounds(value);
            }
        }
    }

    public int Count => _inMemoryStorage.Count;

    /// <summary>
    /// Clears all items stored in memory
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Clear()
    {
        _inMemoryStorage.Clear();
        _lruList.Clear();
    }

    /// <summary>
    /// Clears items stored in memory based on duration passed
    /// </summary>
    /// <param name="duration">TimeSpan to identify expired items</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Clear(TimeSpan duration)
    {
        Clear(DateTime.Now.Subtract(duration));
    }

    /// <summary>
    /// Clears items stored in memory based on duration passed
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Clear(DateTime expirationDate)
    {
        foreach (var (key, node) in _inMemoryStorage)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var item = node.Value;
            if (item.LastUpdated > expirationDate)
            {
                continue;
            }

            Remove(key);
        }
    }

    /// <summary>
    /// Remove items based on provided keys
    /// </summary>
    /// <param name="keys">identified of the in-memory storage item</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Remove(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Remove(key);
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Remove(string key)
    {
        if (!_inMemoryStorage.TryGetValue(key, out var node))
            return;

        _lruList.Remove(node);
        _inMemoryStorage.Remove(key);
    }

    /// <summary>
    /// Add new item to in-memory storage
    /// </summary>
    /// <param name="item">item to be stored</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void SetItem(InMemoryStorageItem<T> item)
    {
        if (MaxItemCount == 0)
        {
            return;
        }

        if (_inMemoryStorage.TryGetValue(item.Id, out var node))
        {
            _lruList.Remove(node);
        }
        else if (_inMemoryStorage.Count >= MaxItemCount)
        {
            RemoveFirst();
        }

        var newNode = new LinkedListNode<InMemoryStorageItem<T>>(item);
        _lruList.AddLast(newNode);
        _inMemoryStorage[item.Id] = newNode;

        /*// ensure max limit is maintained. trim older entries first
        if (_inMemoryStorage.Count > MaxItemCount)
        {
            var itemsToRemove = _inMemoryStorage
                .OrderBy(kvp => kvp.Value.Created)
                .Take(_inMemoryStorage.Count - MaxItemCount)
                .Select(kvp => kvp.Key);
            Remove(itemsToRemove);
        }*/
    }

    /// <summary>
    /// Get item from in-memory storage as long as it has not ex
    /// </summary>
    /// <param name="id">id of the in-memory storage item</param>
    /// <param name="duration">timespan denoting expiration</param>
    /// <returns>Valid item if not out of date or return null if out of date or item does not exist</returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public InMemoryStorageItem<T>? GetItem(string id, TimeSpan duration)
    {
        if (!_inMemoryStorage.TryGetValue(id, out var node))
        {
            return null;
        }

        var expirationDate = DateTime.Now.Subtract(duration);

        if (node.Value.LastUpdated <= expirationDate)
        {
            Remove(id);
            return null;
        }

        _lruList.Remove(node);
        _lruList.AddLast(node);

        return node.Value;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public InMemoryStorageItem<T>? GetItem(string id)
    {
        if (!_inMemoryStorage.TryGetValue(id, out var node))
        {
            return null;
        }

        var value = node.Value;

        _lruList.Remove(node);
        _lruList.AddLast(node);

        return value;
    }

    private void RemoveFirst()
    {
        // Remove from LRUPriority
        var node = _lruList.First;
        _lruList.RemoveFirst();

        if (node == null)
            return;

        // Remove from cache
        _inMemoryStorage.Remove(node.Value.Id);
    }

    private void EnsureStorageBounds(int maxCount)
    {
        if (_inMemoryStorage.Count == 0)
        {
            return;
        }

        if (maxCount == 0)
        {
            _inMemoryStorage.Clear();
            return;
        }

        if (_inMemoryStorage.Count > maxCount)
        {
            Remove(_inMemoryStorage.Keys.Take(_inMemoryStorage.Count - maxCount));
        }
    }
}
