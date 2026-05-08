using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Commons
{
    public class HashLinkedList<TKey,TValue> : IEnumerable<HashLinkedListItem<TKey, TValue>>
    {
        private ConcurrentDictionary<TKey, HashLinkedListItem<TKey,TValue>> _dictionary;
        private HashLinked<TKey,TValue> _linked;

        public int Count => _dictionary.Count;

        public bool Reversed
        {
            get 
            {
                if (_linked == null)
                    return false;
                return _linked.Reversed;
            }
            set
            {
                if(_linked != null)
                    _linked.Reversed = value;
            }
        }

        public HashLinkedListItem<TKey, TValue> Head => _linked.Head;
        public HashLinkedListItem<TKey, TValue> End => _linked.End;

        public TValue this[TKey key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Set(key, value);
            }
        }

        public HashLinkedList()
        {
            _dictionary = new ConcurrentDictionary<TKey, HashLinkedListItem<TKey, TValue>>();
            _linked = new HashLinked<TKey, TValue>();
        }

        public void Add(TKey key, TValue value)
        {
            if(key == null)
                throw new ArgumentNullException("key is null.");
            if (_dictionary.ContainsKey(key))
                throw new ArgumentException("A value with the same key already exists in the HashLinkedList.");
            
            HashLinkedListItem<TKey,TValue> item = new HashLinkedListItem<TKey, TValue>(key,value);
            _dictionary.TryAdd(key, item);
            _linked.Add(item);
        }

        public bool Remove(TKey key)
        {
            if (_dictionary.ContainsKey(key))
            {
                var item = _dictionary[key];
                _dictionary.TryRemove(key,out var v);
                _linked.Remove(item);
                return true;
            }
            return false;
        }

        public void Put(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key is null.");
            Remove(key);
            Add(key, value);
        }

        public void Set(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key is null.");
            if (_dictionary.ContainsKey(key))
            {
                var item = _dictionary[key];
                item.Value = value;
            }
            else
            {
                Add(key, value);
            }
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public TValue Get(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key is null.");

            if (!_dictionary.ContainsKey(key))
                throw new ArgumentException("The key not exists in the HashLinkedList.");

            return _dictionary[key].Value;
        }

        public bool TryGetValue(TKey key,out TValue value)
        {
            value = default(TValue);
            if (key == null)
                return false;
            bool ret = _dictionary.TryGetValue(key, out HashLinkedListItem<TKey,TValue> item);
            value = ret && item != null ? item.Value : default(TValue);
            return ret;
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return _linked.GetEnumerator();
        }

        public IEnumerator<HashLinkedListItem<TKey, TValue>> GetEnumerator()
        {
            return _linked.GetEnumerator();
        }
    }

    public class HashLinkedListItem<TKey, TValue>
    {
        public TKey Key { get; private set; }
        public HashLinkedListItem<TKey, TValue> Previous { get; internal set; }
        public TValue Value { get; set; }
        public HashLinkedListItem<TKey, TValue> Next { get; internal set; }

        public HashLinkedListItem(TKey key,TValue value)
        {
            Key = key;
            Value = value;
            Previous = null;
            Next = null;
        }
    }

    public class HashLinked<TKey,TValue> : IEnumerable<HashLinkedListItem<TKey, TValue>>
    {
        public HashLinkedListItem<TKey,TValue> Head { get; private set; }
        public HashLinkedListItem<TKey,TValue> End { get; private set; }

        public bool Reversed { get; set; }

        public HashLinked()
        {
            Reversed = false;
        }

        public void Add(HashLinkedListItem<TKey,TValue> item)
        {
            if (Head == null)
            {
                Head = item;
            }
            if (this.End != null)
            {
                this.End.Next = item;
                item.Previous = this.End;
            }
            this.End = item;
        }

        public void Add(TKey key,TValue value)
        {
            Add(new HashLinkedListItem<TKey,TValue>(key,value));
        }

        public void Remove(HashLinkedListItem<TKey,TValue> item)
        {
            if (item.Previous != null && item.Next != null)
            {
                item.Previous.Next = item.Next;
                item.Next.Previous = item.Previous;
                return;
            }
            if (item.Previous == null && Head == item)
            {
                Head = item.Next;
                if (item.Next != null)
                    item.Next.Previous = null;
            }
            if (item.Next == null && End == item)
            {
                if(item.Previous != null)
                    item.Previous.Next = null;
                End = item.Previous;
            }
        }

        public IEnumerator<HashLinkedListItem<TKey, TValue>> GetEnumerator()
        {
            return new LinkedListEnumerator(this, Reversed);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new LinkedListEnumerator(this, Reversed);
        }

        public class LinkedListEnumerator : IEnumerator<HashLinkedListItem<TKey, TValue>>
        {
            private HashLinked<TKey, TValue> linkedList;
            private bool reversed = false;
            public LinkedListEnumerator(HashLinked<TKey, TValue> linkedList,bool reversed = false)
            {
                this.linkedList = linkedList;
                //current = this.linkedList.Head;
                this.reversed = reversed;
            }

            private HashLinkedListItem<TKey, TValue> current = null;

            public object Current => current;

            HashLinkedListItem<TKey, TValue> IEnumerator<HashLinkedListItem<TKey, TValue>>.Current => current;

            public void Dispose()
            {
                Reset();
            }

            public bool MoveNext()
            {
                if (reversed)
                {
                    if (current == null)
                    {
                        current = linkedList.End;
                        return current != null;
                    }
                    if (current.Previous == null)
                        return false;
                    current = current.Previous;
                    return true;
                }
                else
                {
                    if (current == null)
                    {
                        current = linkedList.Head;
                        return current != null;
                    }
                    if (current.Next == null)
                        return false;
                    current = current.Next;
                    return true;
                }
            }

            public void Reset()
            {
                current = null;
            }
        }
    }

    
}
