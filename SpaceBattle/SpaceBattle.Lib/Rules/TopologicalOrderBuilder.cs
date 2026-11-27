namespace SpaceBattle.Lib
{
    public sealed class TopologicalOrderBuilder
    {
        public IReadOnlyList<string> Sort(
            string graphName,
            IReadOnlyList<string> nodes,
            IReadOnlyList<(string From, string To)> edges)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);

            if (nodes.Count == 0)
            {
                throw new ArgumentException($"Graph '{graphName}' must contain at least one node.", nameof(nodes));
            }

            var indexByName = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                ArgumentException.ThrowIfNullOrWhiteSpace(node);

                if (!indexByName.TryAdd(node, index))
                {
                    throw new ArgumentException($"Duplicate node '{node}' in graph '{graphName}'.", nameof(nodes));
                }
            }

            var adjacency = new List<int>[nodes.Count];
            for (var index = 0; index < nodes.Count; index++)
            {
                adjacency[index] = new List<int>();
            }

            var inDegree = new int[nodes.Count];
            var seenEdges = new HashSet<(int From, int To)>();

            foreach (var edge in edges)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(edge.From);
                ArgumentException.ThrowIfNullOrWhiteSpace(edge.To);

                if (!indexByName.TryGetValue(edge.From, out var fromIndex))
                {
                    throw new ArgumentException($"Unknown node '{edge.From}' in graph '{graphName}'.", nameof(edges));
                }

                if (!indexByName.TryGetValue(edge.To, out var toIndex))
                {
                    throw new ArgumentException($"Unknown node '{edge.To}' in graph '{graphName}'.", nameof(edges));
                }

                if (fromIndex == toIndex)
                {
                    throw new InvalidOperationException($"Self-loop detected on '{edge.From}' in graph '{graphName}'.");
                }

                if (seenEdges.Add((fromIndex, toIndex)))
                {
                    adjacency[fromIndex].Add(toIndex);
                    inDegree[toIndex]++;
                }
            }

            var ready = new SortedSet<int>();
            for (var index = 0; index < nodes.Count; index++)
            {
                if (inDegree[index] == 0)
                {
                    ready.Add(index);
                }
            }

            var ordered = new List<string>(nodes.Count);
            while (ready.Count > 0)
            {
                var current = ready.Min;
                ready.Remove(current);
                ordered.Add(nodes[current]);

                foreach (var next in adjacency[current])
                {
                    inDegree[next]--;
                    if (inDegree[next] == 0)
                    {
                        ready.Add(next);
                    }
                }
            }

            if (ordered.Count != nodes.Count)
            {
                var remaining = new List<string>();
                for (var index = 0; index < nodes.Count; index++)
                {
                    if (inDegree[index] > 0)
                    {
                        remaining.Add(nodes[index]);
                    }
                }

                throw new InvalidOperationException(
                    $"Cycle detected in graph '{graphName}' involving: {string.Join(", ", remaining)}.");
            }

            return ordered;
        }

        public IReadOnlyList<string> SortActionSteps(
            string actionName,
            IReadOnlyList<string> steps,
            IReadOnlyList<string> orderConstraints)
        {
            ArgumentNullException.ThrowIfNull(orderConstraints);

            var parsedEdges = new List<(string From, string To)>(orderConstraints.Count);
            foreach (var constraint in orderConstraints)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(constraint);

                var parts = constraint.Split(" before ", StringSplitOptions.None);
                if (parts.Length != 2)
                {
                    throw new ArgumentException(
                        $"Invalid order constraint '{constraint}' in action '{actionName}'. Expected '<A> before <B>'.",
                        nameof(orderConstraints));
                }

                parsedEdges.Add((parts[0], parts[1]));
            }

            return Sort(actionName, steps, parsedEdges);
        }
    }
}
