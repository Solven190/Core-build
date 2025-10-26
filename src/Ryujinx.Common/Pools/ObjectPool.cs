using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ryujinx.Common
{
    public class ObjectPool<T>(Func<T> factory, int size = -1)
        where T : class
    {
        private int _size = size;
        private readonly ConcurrentStack<T> _items = new();

        public T Allocate()
        {
            bool success = _items.TryPop(out T instance);

            if (!success)
            {
                instance =  factory();
            }

            return instance;
        }

        public void Release(T obj)
        {
            if (_size < 0 || _items.Count < _size)
            {
                _items.Push(obj);
            }
        }
        
        public void Clear() => _items.Clear();
    }
}
