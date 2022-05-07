using Faucet.Helpers;
using Dawn;

namespace Faucet.Persistence;

public class Caching<TItem> 
{
    private Dictionary<byte[], TItem> _innerDictionary = new(BinaryComparer.Default);
    private static readonly object Locking = new();
    
    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    public TItem this[byte[] key]
    {
        get
        {
            lock (Locking)
            {
                try
                {
                    return _innerDictionary[key];
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }
    }

    /// <summary>
    /// </summary>
    public int Count
    {
        get
        {
            lock (Locking)
            {
                return _innerDictionary.Count;
            }
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    /// <param name="item"></param>
    public void Add(byte[] key, TItem item)
    {
        lock (Locking)
        {
            if (!_innerDictionary.TryGetValue(key, out _)) _innerDictionary.Add(key, item);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    /// <param name="item"></param>
    public bool AddOrUpdate(byte[] key, TItem item)
    {
        lock (Locking)
        {
            if (_innerDictionary.TryGetValue(key, out _))
            {
                _innerDictionary[key] = item;
                return true;
            }
            _innerDictionary.Add(key, item);
            return true;
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    public bool Remove(byte[] key)
    {
        lock (Locking)
        {
            if (!_innerDictionary.TryGetValue(key, out var cachedItem)) return false;
            _innerDictionary.Remove(key);
            if (cachedItem is not IDisposable disposable) return true;
            disposable.Dispose();
            return true;
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryGet(byte[] key, out TItem item)
    {
        lock (Locking)
        {
            if (_innerDictionary.TryGetValue(key, out var cacheItem))
            {
                item = cacheItem;
                return true;
            }
            
            item = default;
            return false;
        }
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public TItem[] GetItems()
    {
        lock (Locking)
        {
            var items = new TItem[_innerDictionary.Count];
            _innerDictionary.Values.CopyTo(items, 0);
            return items;
        }
    }

    /// <summary>
    /// </summary>
    public void Clear()
    {
        lock (Locking)
        {
            foreach (var (key, _) in _innerDictionary) Remove(key);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool Contains(byte[] key)
    {
        lock (Locking)
        {
            return _innerDictionary.TryGetValue(key, out _);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public ValueTask<KeyValuePair<byte[], TItem>[]> WhereAsync(
        Func<KeyValuePair<byte[], TItem>, ValueTask<bool>> expression)
    {
        Guard.Argument(expression, nameof(expression)).NotNull();

        lock (Locking)
        {
            var entries = Iterate().WhereAwait(expression).ToArrayAsync();
            return entries;
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public IEnumerable<KeyValuePair<byte[], TItem>> Where(Func<KeyValuePair<byte[], TItem>, bool> expression)
    {
        Guard.Argument(expression, nameof(expression)).NotNull();

        lock (Locking)
        {
            var entries = Iterate().Where(expression).ToEnumerable();
            return entries;
        }
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    private IAsyncEnumerable<KeyValuePair<byte[], TItem>> Iterate()
    {
        lock (Locking)
        {
            return _innerDictionary.ToAsyncEnumerable();
        }
    }
}