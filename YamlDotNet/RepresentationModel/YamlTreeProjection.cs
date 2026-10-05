// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) Antoine Aubry and contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Projects a <see cref="YamlNode"/> graph (which may contain shared nodes introduced by
    /// anchors and aliases) into a self-contained tree in which every node is a new, unshared
    /// instance. The projected tree contains no anchors and no aliases, which makes it suitable
    /// for downstream consumers that only accept alias-free YAML.
    /// </summary>
    /// <remarks>
    /// The projection runs in two phases. The first phase walks the source graph without
    /// allocating any result: it computes the exact number of nodes that the expanded tree
    /// would contain and detects genuine cycles. A node that is merely shared (referenced from
    /// several parents) is counted once per occurrence but is never mistaken for a cycle; only
    /// a node that is its own ancestor is a cycle. The second phase runs only when the expanded
    /// node count fits the specified budget, and copies each visited node, preserving tags and
    /// styles while dropping anchors. The source graph is never modified.
    /// </remarks>
    public static class YamlTreeProjection
    {
        /// <summary>
        /// Projects the specified node graph into a tree.
        /// </summary>
        /// <param name="root">The root of the graph to project. The graph is not modified.</param>
        /// <param name="maxNodes">The maximum number of nodes that the expanded tree may contain.</param>
        /// <returns>The projection result, including provenance from each copy to its original node.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxNodes"/> is negative.</exception>
        /// <exception cref="NotSupportedException">The graph contains a node of an unsupported (extension) type.</exception>
        /// <exception cref="YamlTreeProjectionException">
        /// The expansion would exceed <paramref name="maxNodes"/> nodes, or the graph contains a cycle.
        /// </exception>
        public static YamlTreeProjectionResult Project(YamlNode root, int maxNodes)
        {
            var status = TryProject(root, maxNodes, out var result);
            return status == YamlTreeProjectionStatus.Success
                ? result!
                : throw new YamlTreeProjectionException(status, maxNodes);
        }

        /// <summary>
        /// Attempts to project the specified node graph into a tree.
        /// When the returned status is not <see cref="YamlTreeProjectionStatus.Success"/>,
        /// <paramref name="result"/> is <see langword="null"/> and no result tree was allocated.
        /// </summary>
        /// <param name="root">The root of the graph to project. The graph is not modified.</param>
        /// <param name="maxNodes">The maximum number of nodes that the expanded tree may contain.</param>
        /// <param name="result">When successful, the projection result; otherwise, <see langword="null"/>.</param>
        /// <returns>A value that describes the outcome of the projection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxNodes"/> is negative.</exception>
        /// <exception cref="NotSupportedException">The graph contains a node of an unsupported (extension) type.</exception>
        public static YamlTreeProjectionStatus TryProject(YamlNode root, int maxNodes, out YamlTreeProjectionResult? result)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (maxNodes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNodes), maxNodes, "The node budget cannot be negative.");
            }

            result = null;

            // Phase 1: analyze the source graph without allocating any result.
            var analyzer = new ExpansionAnalyzer(maxNodes);
            var status = analyzer.Analyze(root, out var nodeCount);
            if (status != YamlTreeProjectionStatus.Success)
            {
                return status;
            }

            // Phase 2: the expansion fits the budget and is acyclic; copy the nodes.
            var provenance = new Dictionary<YamlNode, YamlNode>(NodeIdentityComparer.Instance);
            var projectedRoot = CopyNode(root, provenance);
            result = new YamlTreeProjectionResult(root, projectedRoot, nodeCount, provenance);
            return YamlTreeProjectionStatus.Success;
        }

        private static YamlNode CopyNode(YamlNode node, Dictionary<YamlNode, YamlNode> provenance)
        {
            switch (node)
            {
                case YamlScalarNode scalar:
                    var scalarCopy = new YamlScalarNode
                    {
                        Value = scalar.Value,
                        Style = scalar.Style,
                        Tag = scalar.Tag,
                        MaxDepth = scalar.MaxDepth,
                    };
                    provenance.Add(scalarCopy, node);
                    return scalarCopy;

                case YamlSequenceNode sequence:
                    var sequenceCopy = new YamlSequenceNode
                    {
                        Style = sequence.Style,
                        Tag = sequence.Tag,
                        MaxDepth = sequence.MaxDepth,
                    };
                    provenance.Add(sequenceCopy, node);
                    foreach (var child in sequence.Children)
                    {
                        sequenceCopy.Add(CopyNode(child, provenance));
                    }

                    return sequenceCopy;

                case YamlMappingNode mapping:
                    var mappingCopy = new YamlMappingNode
                    {
                        Style = mapping.Style,
                        Tag = mapping.Tag,
                        MaxDepth = mapping.MaxDepth,
                    };
                    provenance.Add(mappingCopy, node);
                    foreach (var pair in mapping.Children)
                    {
                        mappingCopy.Add(CopyNode(pair.Key, provenance), CopyNode(pair.Value, provenance));
                    }

                    return mappingCopy;

                default:
                    throw UnsupportedNode(node);
            }
        }

        private static NotSupportedException UnsupportedNode(YamlNode node)
        {
            return new NotSupportedException(
                $"Nodes of type '{node.GetType().FullName}' cannot be projected. " +
                $"Only {nameof(YamlScalarNode)}, {nameof(YamlSequenceNode)} and {nameof(YamlMappingNode)} are supported; " +
                "unresolved aliases and unknown extension nodes are rejected.");
        }

        /// <summary>
        /// Computes the exact number of nodes that the expanded tree would contain,
        /// counting shared nodes once per occurrence, and detects genuine cycles.
        /// </summary>
        private sealed class ExpansionAnalyzer
        {
            private readonly Dictionary<YamlNode, long> completedCounts = new Dictionary<YamlNode, long>(NodeIdentityComparer.Instance);
            private readonly HashSet<YamlNode> activeNodes = new HashSet<YamlNode>(NodeIdentityComparer.Instance);
            private readonly long saturationPoint;

            public ExpansionAnalyzer(int maxNodes)
            {
                // Counts saturate at maxNodes + 1: any count above the budget is
                // equivalent for the accept/reject decision and cannot overflow.
                saturationPoint = (long)maxNodes + 1;
            }

            public YamlTreeProjectionStatus Analyze(YamlNode root, out long nodeCount)
            {
                try
                {
                    nodeCount = CountNodes(root);
                }
                catch (CycleDetectedException)
                {
                    nodeCount = 0;
                    return YamlTreeProjectionStatus.CycleDetected;
                }

                return nodeCount < saturationPoint
                    ? YamlTreeProjectionStatus.Success
                    : YamlTreeProjectionStatus.BudgetExceeded;
            }

            private long CountNodes(YamlNode node)
            {
                // A node whose subtree was fully counted is acyclic and always expands
                // to the same number of nodes, no matter how many parents reference it.
                if (completedCounts.TryGetValue(node, out var completedCount))
                {
                    return completedCount;
                }

                // A node that is still on the recursion stack is its own ancestor:
                // this is a genuine cycle, not a mere shared reference.
                if (!activeNodes.Add(node))
                {
                    throw new CycleDetectedException();
                }

                long count;
                switch (node)
                {
                    case YamlScalarNode _:
                        count = 1;
                        break;

                    case YamlSequenceNode sequence:
                        count = 1;
                        foreach (var child in sequence.Children)
                        {
                            count = SaturatingAdd(count, CountNodes(child));
                        }

                        break;

                    case YamlMappingNode mapping:
                        count = 1;
                        foreach (var pair in mapping.Children)
                        {
                            count = SaturatingAdd(count, CountNodes(pair.Key));
                            count = SaturatingAdd(count, CountNodes(pair.Value));
                        }

                        break;

                    default:
                        activeNodes.Remove(node);
                        throw UnsupportedNode(node);
                }

                activeNodes.Remove(node);
                completedCounts.Add(node, count);
                return count;
            }

            private long SaturatingAdd(long left, long right)
            {
                var sum = left + right;
                return sum >= saturationPoint ? saturationPoint : sum;
            }
        }

        private sealed class CycleDetectedException : Exception
        {
        }

        /// <summary>
        /// Compares nodes by reference identity, so that two distinct but equal nodes
        /// are never confused with one another.
        /// </summary>
        private sealed class NodeIdentityComparer : IEqualityComparer<YamlNode>
        {
            public static readonly NodeIdentityComparer Instance = new NodeIdentityComparer();

            private NodeIdentityComparer()
            {
            }

            public bool Equals(YamlNode? x, YamlNode? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(YamlNode obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
