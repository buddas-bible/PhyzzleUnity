using System;
using System.Collections.Generic;

namespace Phyzzle.Abilities.Rewind
{
    public sealed class FixedRingBuffer<T>
    {
        private readonly T[] values;
        private int start;

        public int Count { get; private set; }
        public int Capacity => values.Length;

        public FixedRingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            values = new T[capacity];
        }

        public void Add(T value)
        {
            if (Count < Capacity)
            {
                values[(start + Count) % Capacity] = value;
                Count++;
                return;
            }

            values[start] = value;
            start = (start + 1) % Capacity;
        }

        public T GetFromOldest(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return values[(start + index) % Capacity];
        }

        public IReadOnlyList<T> CopyToList()
        {
            List<T> result = new(Count);
            for (int i = 0; i < Count; i++)
            {
                result.Add(GetFromOldest(i));
            }

            return result;
        }

        public void Clear()
        {
            Array.Clear(values, 0, values.Length);
            start = 0;
            Count = 0;
        }
    }
}
