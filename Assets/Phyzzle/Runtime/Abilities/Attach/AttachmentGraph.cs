using System.Collections.Generic;

namespace Phyzzle.Abilities.Attach
{
    public sealed class AttachmentGraph<T>
    {
        private readonly Dictionary<T, HashSet<T>> adjacency = new();

        public int NodeCount => adjacency.Count;

        public void Add(T node)
        {
            if (!adjacency.ContainsKey(node))
            {
                adjacency.Add(node, new HashSet<T>());
            }
        }

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

        public bool Disconnect(T first, T second)
        {
            bool firstRemoved = adjacency.TryGetValue(first, out HashSet<T> firstNeighbors) &&
                                firstNeighbors.Remove(second);
            bool secondRemoved = adjacency.TryGetValue(second, out HashSet<T> secondNeighbors) &&
                                 secondNeighbors.Remove(first);
            return firstRemoved || secondRemoved;
        }

        public bool AreDirectlyConnected(T first, T second)
        {
            return adjacency.TryGetValue(first, out HashSet<T> neighbors) && neighbors.Contains(second);
        }

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

        public IReadOnlyCollection<T> GetNeighbors(T node)
        {
            if (!adjacency.TryGetValue(node, out HashSet<T> neighbors))
            {
                return System.Array.Empty<T>();
            }

            return new List<T>(neighbors);
        }

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
