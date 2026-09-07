using System;
using System.Collections.Generic;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 고정 용량을 초과하면 가장 오래된 값을 덮어쓰는 순환 버퍼다.
    /// </summary>
    public sealed class FixedRingBuffer<T>
    {
        private readonly T[] values;
        private int start;

        public int Count { get; private set; }
        public int Capacity => values.Length;

        /// <summary>
        /// 지정한 고정 용량으로 순환 버퍼를 생성한다.
        /// </summary>
        public FixedRingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            values = new T[capacity];
        }

        /// <summary>
        /// 새 값을 추가하고 용량이 가득 찬 경우 가장 오래된 값을 덮어쓴다.
        /// </summary>
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

        /// <summary>
        /// 가장 오래된 값을 0번으로 하는 논리 인덱스로 항목을 반환한다.
        /// </summary>
        public T GetFromOldest(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return values[(start + index) % Capacity];
        }

        /// <summary>
        /// 현재 저장된 항목을 오래된 순서대로 새 목록에 복사한다.
        /// </summary>
        public IReadOnlyList<T> CopyToList()
        {
            List<T> result = new(Count);
            for (int i = 0; i < Count; i++)
            {
                result.Add(GetFromOldest(i));
            }

            return result;
        }

        /// <summary>
        /// 저장된 값을 모두 지우고 버퍼의 시작 위치와 개수를 초기화한다.
        /// </summary>
        public void Clear()
        {
            Array.Clear(values, 0, values.Length);
            start = 0;
            Count = 0;
        }
    }
}
