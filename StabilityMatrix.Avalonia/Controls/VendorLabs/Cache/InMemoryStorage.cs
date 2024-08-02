// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StabilityMatrix.Avalonia.Controls.VendorLabs.Cache;

/// <summary>
/// Generic in-memory storage of items
/// </summary>
/// <typeparam name="T">T defines the type of item stored</typeparam>
public class InMemoryStorage<T>
{
    private int _maxItemCount;
    private ConcurrentDictionary<string, InMemoryStorageItem<T>> _inMemoryStorage =
        new ConcurrentDictionary<string, InMemoryStorageItem<T>>();
    private object _settingMaxItemCountLocker = new object();

    /// <summary>
    /// Gets or sets the maximum count of Items that can be stored in this InMemoryStorage instance.
    /// </summary>
    public int MaxItemCount
    {
        get { return _maxItemCount; }
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

    /// <summary>
    /// Clears all items stored in memory
    /// </summary>
    public void Clear()
    {
        _inMemoryStorage.Clear();
    }

    /// <summary>
    /// Clears items stored in memory based on duration passed
    /// </summary>
    /// <param name="duration">TimeSpan to identify expired items</param>
    public void Clear(TimeSpan duration)
    {
        var expirationDate = DateTime.Now.Subtract(duration);

        var itemsToRemove = _inMemoryStorage
            .Where(kvp => kvp.Value.LastUpdated <= expirationDate)
            .Select(kvp => kvp.Key);

        if (itemsToRemove.Any())
        {
            Remove(itemsToRemove);
        }
    }

    /// <summary>
    /// Remove items based on provided keys
    /// </summary>
    /// <param name="keys">identified of the in-memory storage item</param>
    public void Remove(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            _inMemoryStorage.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Add new item to in-memory storage
    /// </summary>
    /// <param name="item">item to be stored</param>
    public void SetItem(InMemoryStorageItem<T> item)
    {
        if (MaxItemCount == 0)
        {
            return;
        }

        _inMemoryStorage[item.Id] = item;

        // ensure max limit is maintained. trim older entries first
        if (_inMemoryStorage.Count > MaxItemCount)
        {
            var itemsToRemove = _inMemoryStorage
                .OrderBy(kvp => kvp.Value.Created)
                .Take(_inMemoryStorage.Count - MaxItemCount)
                .Select(kvp => kvp.Key);
            Remove(itemsToRemove);
        }
    }

    /// <summary>
    /// Get item from in-memory storage as long as it has not ex
    /// </summary>
    /// <param name="id">id of the in-memory storage item</param>
    /// <param name="duration">timespan denoting expiration</param>
    /// <returns>Valid item if not out of date or return null if out of date or item does not exist</returns>
    public InMemoryStorageItem<T>? GetItem(string id, TimeSpan duration)
    {
        if (!_inMemoryStorage.TryGetValue(id, out var tempItem))
        {
            return null;
        }

        var expirationDate = DateTime.Now.Subtract(duration);

        if (tempItem.LastUpdated > expirationDate)
        {
            return tempItem;
        }

        _inMemoryStorage.TryRemove(id, out _);

        return null;
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
