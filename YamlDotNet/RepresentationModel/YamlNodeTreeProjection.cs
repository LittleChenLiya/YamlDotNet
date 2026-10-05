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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Projects a <see cref="YamlNode"/> graph into an alias-free tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The representation model allows the same node instance to be referenced multiple times (shared nodes),
    /// and even allows cycles. Some downstream consumers only accept alias-free YAML trees, where each node
    /// appears exactly once and no anchors or aliases are used. This projection performs that transformation:
    /// </para>
    /// <list type="bullet">
    /// <item>Shared nodes are copied into independent nodes, one copy per occurrence in the graph.</item>
    /// <item>Each copy records the original node it was created from in <see cref="YamlNode.OriginalNode"/>.</item>
    /// <item>Tags and scalar styles are preserved; anchors and aliases are not emitted.</item>
    /// <item>Unknown extension nodes (such as <see cref="YamlAliasNode"/>) are rejected.</item>
    /// </list>
    /// <para>
    /// Before allocating any result, the projection first computes the total number of nodes that the projected
    /// tree would contain and detects cycles on the node graph. If the expansion count exceeds the specified
    /// budget, no result is allocated. Shared references (a node reached through multiple paths) are not mistaken
    /// for cycles; only a back edge to a node currently being visited is considered a cycle.
    /// </para>
    /// <para>
    /// The source graph and its anchors are never modified. The projected tree contains no anchors or aliases.
    /// </para>
    /// </remarks>
    public sealed class YamlNodeTreeProjection
    {
        private readonly Dictionary<YamlNode, int> subtreeCounts = new();
        private readonly HashSet<YamlNode> cycleStack = new();

        private YamlNodeTreeProjection()
        {
        }

        /// <summary>
        /// Projects the specified root node into an alias-free tree, copying shared nodes into independent nodes.
        /// </summary>
        /// <param name="root">The root node of the source graph.</param>
        /// <param name="budget">The maximum number of nodes that the projected tree may contain.</param>
        /// <returns>The root node of the projected alias-free tree.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="budget"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">
        /// The projected tree would contain more than <paramref name="budget"/> nodes, or a cycle was detected in the source graph.
        /// </exception>
        /// <exception cref="NotSupportedException">An unknown extension node was encountered in the source graph.</exception>
        public static YamlNode Project(YamlNode root, int budget)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (budget < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(budget), "The budget must be a non-negative value.");
            }

            var projection = new YamlNodeTreeProjection();
            var count = projection.Analyze(root);
            if (count > budget)
            {
                throw new InvalidOperationException($"The projected tree would contain {count} nodes, which exceeds the budget of {budget}. No result was allocated.");
            }

            return YamlNodeTreeProjection.Allocate(root);
        }

        /// <summary>
        /// Tries to project the specified root node into an alias-free tree.
        /// </summary>
        /// <param name="root">The root node of the source graph.</param>
        /// <param name="budget">The maximum number of nodes that the projected tree may contain.</param>
        /// <param name="result">When successful, the root node of the projected alias-free tree; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the projection succeeded within the budget; otherwise, <c>false</c>.</returns>
        public static bool TryProject(YamlNode root, int budget, out YamlNode? result)
        {
            result = null;
            if (root == null || budget < 0)
            {
                return false;
            }

            var projection = new YamlNodeTreeProjection();
            var count = projection.Analyze(root);
            if (count > budget)
            {
                return false;
            }

            result = YamlNodeTreeProjection.Allocate(root);
            return true;
        }

        /// <summary>
        /// Computes the number of nodes that the projected tree would contain, without allocating any result.
        /// </summary>
        /// <param name="root">The root node of the source graph.</param>
        /// <returns>The total number of nodes in the projected tree.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">A cycle was detected in the source graph.</exception>
        /// <exception cref="NotSupportedException">An unknown extension node was encountered in the source graph.</exception>
        /// <remarks>
        /// This method performs the analysis phase only. It can be used to inspect the expansion count before
        /// deciding on a budget. Shared references are counted once per occurrence; cycles are detected and reported.
        /// </remarks>
        public static int CountExpansion(YamlNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var projection = new YamlNodeTreeProjection();
            return projection.Analyze(root);
        }

        /// <summary>
        /// Analyzes the source graph: computes the number of nodes that the projected tree would contain and detects cycles.
        /// </summary>
        /// <param name="node">The current node being analyzed.</param>
        /// <returns>The number of nodes produced from one occurrence of <paramref name="node"/>.</returns>
        private int Analyze(YamlNode node)
        {
            // Reject unknown extension nodes. Only the three core node types are supported.
            if (node.NodeType != YamlNodeType.Scalar &&
                node.NodeType != YamlNodeType.Sequence &&
                node.NodeType != YamlNodeType.Mapping)
            {
                throw new NotSupportedException($"The node type '{node.NodeType}' is not supported by the tree projection. Only scalar, sequence, and mapping nodes are supported.");
            }

            // Cycle detection: if this node is already on the current DFS stack, we have a back edge, which is a cycle.
            // Shared references (a node reached through multiple paths that is not on the current stack) are not cycles.
            if (!cycleStack.Add(node))
            {
                throw new InvalidOperationException("A cycle was detected in the node graph. The tree projection cannot expand a cyclic graph.");
            }

            try
            {
                // If this node's subtree count has already been computed, reuse it. This happens when a shared node
                // is reached through a second path. The cached count is the number of nodes produced from one occurrence.
                if (subtreeCounts.TryGetValue(node, out var cachedCount))
                {
                    return cachedCount;
                }

                var count = 1; // this node
                switch (node)
                {
                    case YamlSequenceNode sequence:
                        foreach (var child in sequence.Children)
                        {
                            count += Analyze(child);
                        }
                        break;

                    case YamlMappingNode mapping:
                        foreach (var entry in mapping.Children)
                        {
                            count += Analyze(entry.Key);
                            count += Analyze(entry.Value);
                        }
                        break;
                }

                subtreeCounts[node] = count;
                return count;
            }
            finally
            {
                cycleStack.Remove(node);
            }
        }

        /// <summary>
        /// Allocates the projected tree by copying each node into an independent node.
        /// </summary>
        /// <param name="node">The current source node being copied.</param>
        /// <returns>A new independent copy of <paramref name="node"/>.</returns>
        private static YamlNode Allocate(YamlNode node)
        {
            // Reject unknown extension nodes defensively (Analyze should already have caught these).
            if (node.NodeType != YamlNodeType.Scalar &&
                node.NodeType != YamlNodeType.Sequence &&
                node.NodeType != YamlNodeType.Mapping)
            {
                throw new NotSupportedException($"The node type '{node.NodeType}' is not supported by the tree projection.");
            }

            YamlNode copy;
            switch (node)
            {
                case YamlScalarNode scalar:
                    copy = new YamlScalarNode(scalar.Value)
                    {
                        Tag = scalar.Tag,
                        Style = scalar.Style,
                        OriginalNode = node
                    };
                    break;

                case YamlSequenceNode sequence:
                    var sequenceCopy = new YamlSequenceNode
                    {
                        Tag = sequence.Tag,
                        Style = sequence.Style,
                        OriginalNode = node
                    };
                    foreach (var child in sequence.Children)
                    {
                        sequenceCopy.Add(Allocate(child));
                    }
                    copy = sequenceCopy;
                    break;

                case YamlMappingNode mapping:
                    var mappingCopy = new YamlMappingNode
                    {
                        Tag = mapping.Tag,
                        Style = mapping.Style,
                        OriginalNode = node
                    };
                    foreach (var entry in mapping.Children)
                    {
                        mappingCopy.Add(Allocate(entry.Key), Allocate(entry.Value));
                    }
                    copy = mappingCopy;
                    break;

                default:
                    throw new NotSupportedException($"The node type '{node.NodeType}' is not supported by the tree projection.");
            }

            return copy;
        }
    }
}
