using System.Collections.Generic;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 오브젝트 사이의 양방향 연결과 연결 컴포넌트를 관리하는 범용 그래프다.
    /// </summary>
    public sealed class AttachmentGraph<T>
    {
        private readonly Dictionary<T, HashSet<T>> adjacency = new();

        public int NodeCount => adjacency.Count;

        /// <summary>
        /// 그래프에 지정한 노드가 없으면 새 노드로 추가한다.
        /// </summary>
        public void Add(T node)
        {
            if (!adjacency.ContainsKey(node))
            {
                adjacency.Add(node, new HashSet<T>());
            }
        }

        /// <summary>
        /// 지정한 노드와 연결된 간선을 제거한 뒤 그래프에서 노드를 삭제한다.
        /// </summary>
        public bool Remove(T node)
        {
            if (!adjacency.TryGetValue(node, out HashSet<T> neighbors))
            {
                return false;
            }

            foreach (T neighbor in neighbors)
            {
                adjacency[neighbor].Remove(node);
            }

            return adjacency.Remove(node);
        }

        /// <summary>
        /// 두 노드를 그래프에 등록하고 양방향 간선으로 연결한다.
        /// </summary>
        public bool Connect(T first, T second)
        {
            if (EqualityComparer<T>.Default.Equals(first, second))
            {
                return false;
            }

            Add(first);
            Add(second);
            bool firstAdded = adjacency[first].Add(second);
            bool secondAdded = adjacency[second].Add(first);
            return firstAdded || secondAdded;
        }

        /// <summary>
        /// 두 노드 사이의 양방향 직접 연결을 해제한다.
        /// </summary>
        public bool Disconnect(T first, T second)
        {
            bool firstRemoved = adjacency.TryGetValue(first, out HashSet<T> firstNeighbors) &&
                                firstNeighbors.Remove(second);
            bool secondRemoved = adjacency.TryGetValue(second, out HashSet<T> secondNeighbors) &&
                                 secondNeighbors.Remove(first);
            return firstRemoved || secondRemoved;
        }

        /// <summary>
        /// 두 노드가 하나의 간선으로 직접 연결되어 있는지 확인한다.
        /// </summary>
        public bool AreDirectlyConnected(T first, T second)
        {
            return adjacency.TryGetValue(first, out HashSet<T> neighbors) && neighbors.Contains(second);
        }

        /// <summary>
        /// 두 노드가 같은 연결 컴포넌트에 속하는지 탐색한다.
        /// </summary>
        public bool AreInSameComponent(T first, T second)
        {
            if (EqualityComparer<T>.Default.Equals(first, second))
            {
                return true;
            }

            foreach (T node in GetComponent(first))
            {
                if (EqualityComparer<T>.Default.Equals(node, second))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 지정한 노드에 직접 연결된 이웃 노드의 복사본을 반환한다.
        /// </summary>
        public IReadOnlyCollection<T> GetNeighbors(T node)
        {
            if (!adjacency.TryGetValue(node, out HashSet<T> neighbors))
            {
                return System.Array.Empty<T>();
            }

            return new List<T>(neighbors);
        }

        /// <summary>
        /// 시작 노드에서 도달 가능한 모든 노드를 너비 우선 탐색으로 수집한다.
        /// </summary>
        public IReadOnlyCollection<T> GetComponent(T start)
        {
            if (!adjacency.ContainsKey(start))
            {
                return new[] { start };
            }

            List<T> result = new();
            HashSet<T> visited = new();
            Queue<T> search = new();
            visited.Add(start);
            search.Enqueue(start);

            while (search.Count > 0)
            {
                T current = search.Dequeue();
                result.Add(current);
                foreach (T neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                    {
                        search.Enqueue(neighbor);
                    }
                }
            }

            return result;
        }
    }
}
