using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Commons
{
    public class LRU<TKey, TValue> : IEnumerable<HashLinkedListItem<TKey, TValue>>
    {
        public int Capacity { get; private set; }
        private HashLinkedList<TKey, TValue> _cache;

        public HashLinkedListItem<TKey, TValue> Head => _cache.Head;
        public HashLinkedListItem<TKey, TValue> End => _cache.End;

        private readonly object LockObj = new object();

        public LRU(int capacity)
        {
            Capacity = capacity;
            _cache = new HashLinkedList<TKey, TValue>();
        }

        public TValue Get(TKey key)
        {
            lock (LockObj)
            {
                if (_cache.TryGetValue(key, out TValue value))
                {
                    _cache.Put(key, value);
                    return value;
                }
            }
            throw new ArgumentException("The key not exists in the HashLinkedList.");
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (LockObj)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    _cache.Put(key, value);
                    return true;
                }
                return false;
            }
        }

        public bool ContainsKey(TKey key) => _cache.ContainsKey(key);

        public void Put(TKey key, TValue value)
        {
            lock (LockObj)
            {
                //_cache[key] = value;
                _cache.Put(key, value);

                if (_cache.Count > Capacity)
                {
                    _cache.Remove(_cache.Head.Key);

                }
            }
        }

        public void Remove(TKey key)
        {
            lock (LockObj)
            {
                _cache.Remove(key);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cache.GetEnumerator();
        }

        public IEnumerator<HashLinkedListItem<TKey, TValue>> GetEnumerator()
        {
            return _cache.GetEnumerator();
        }
    }
}
