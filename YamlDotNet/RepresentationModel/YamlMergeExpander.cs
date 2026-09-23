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
using System.Text;
using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Expands the merge keys (<c>&lt;&lt;</c>) of a YAML node tree, producing a new,
    /// independent tree in which every merged value is materialized, and tracking the
    /// origin of each resulting value.
    /// <para>
    /// This is an opt-in alternative to <see cref="MergingParser"/>: while
    /// <see cref="MergingParser"/> rewrites the event stream during parsing without
    /// keeping any provenance information, <see cref="YamlMergeExpander"/> operates on
    /// the representation model after parsing, leaves the input tree unchanged, and
    /// reports where each resulting value was defined.
    /// </para>
    /// </summary>
    public static class YamlMergeExpander
    {
        /// <summary>
        /// Expands the merge keys of the specified node tree.
        /// </summary>
        /// <param name="root">The root of the tree to expand. The tree is not modified.</param>
        /// <param name="options">Options that control the expansion, or <see langword="null"/> to use the defaults.</param>
        /// <returns>The expanded tree along with the origin of each resulting value.</returns>
        /// <exception cref="YamlException">
        /// Thrown when a mapping contains a non-scalar or duplicate explicit key, when a merge
        /// value is not an alias or a sequence of aliases to mappings, when the anchors form a
        /// cycle along the current expansion path, or when the expansion budget is exceeded.
        /// </exception>
        public static YamlMergeExpansionResult Expand(YamlNode root, MergeExpansionOptions? options = null)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var state = new ExpansionState((options ?? new MergeExpansionOptions()).MaxExpandedNodes);
            var path = new List<AnchorFrame>();
            var expandedRoot = state.ExpandNode(root, path, root.Start);
            state.PruneOrigins(expandedRoot);
            return new YamlMergeExpansionResult(expandedRoot, state.Origins);
        }

        /// <summary>
        /// Expands the merge keys of the root node of the specified document.
        /// </summary>
        /// <param name="document">The document to expand. The document is not modified.</param>
        /// <param name="options">Options that control the expansion, or <see langword="null"/> to use the defaults.</param>
        /// <returns>The expanded tree along with the origin of each resulting value.</returns>
        public static YamlMergeExpansionResult Expand(YamlDocument document, MergeExpansionOptions? options = null)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return Expand(document.RootNode, options);
        }

        private readonly struct AnchorFrame
        {
            public AnchorFrame(YamlNode node, AnchorName traversedAnchor)
            {
                Node = node;
                TraversedAnchor = traversedAnchor;
            }

            public YamlNode Node { get; }

            /// <summary>
            /// The anchor through which this node was reached, or <see cref="AnchorName.Empty"/>
            /// when the node was reached directly.
            /// </summary>
            public AnchorName TraversedAnchor { get; }
        }

        private readonly struct MergeSource
        {
            public MergeSource(YamlMappingNode mapping, AnchorName anchor, Mark usedAt)
            {
                Mapping = mapping;
                Anchor = anchor;
                UsedAt = usedAt;
            }

            public YamlMappingNode Mapping { get; }
            public AnchorName Anchor { get; }
            public Mark UsedAt { get; }
        }

        private sealed class NodeReferenceEqualityComparer : IEqualityComparer<YamlNode>
        {
            public static readonly NodeReferenceEqualityComparer Instance = new NodeReferenceEqualityComparer();

            private NodeReferenceEqualityComparer()
            {
            }

            public bool Equals(YamlNode? x, YamlNode? y) => ReferenceEquals(x, y);

            public int GetHashCode(YamlNode obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private sealed class ExpansionState
        {
            private readonly int maxExpandedNodes;
            private readonly Dictionary<YamlNode, YamlNodeOrigin> origins = new Dictionary<YamlNode, YamlNodeOrigin>(NodeReferenceEqualityComparer.Instance);
            private int expandedNodeCount;

            public ExpansionState(int maxExpandedNodes)
            {
                this.maxExpandedNodes = maxExpandedNodes;
            }

            public IReadOnlyDictionary<YamlNode, YamlNodeOrigin> Origins => origins;

            public YamlNode ExpandNode(YamlNode node, List<AnchorFrame> path, Mark usedAt)
            {
                switch (node)
                {
                    case YamlScalarNode scalar:
                        ConsumeBudget(scalar);
                        return new YamlScalarNode(scalar.Value)
                        {
                            Tag = scalar.Tag,
                            Style = scalar.Style,
                        };

                    case YamlSequenceNode sequence:
                        return ExpandSequence(sequence, path);

                    case YamlMappingNode mapping:
                        return ExpandMapping(mapping, path, AnchorName.Empty, usedAt);

                    case YamlAliasNode alias:
                        throw new YamlException(alias.Start, alias.End, $"Cannot expand the unresolved alias '{alias.Anchor}'.");

                    default:
                        throw new YamlException(node.Start, node.End, $"Cannot expand a node of type '{node.GetType().Name}'.");
                }
            }

            private YamlSequenceNode ExpandSequence(YamlSequenceNode sequence, List<AnchorFrame> path)
            {
                EnterNode(sequence, path, AnchorName.Empty);
                try
                {
                    ConsumeBudget(sequence);
                    var result = new YamlSequenceNode
                    {
                        Tag = sequence.Tag,
                        Style = sequence.Style,
                    };

                    foreach (var child in sequence.Children)
                    {
                        result.Add(ExpandNode(child, path, child.Start));
                    }

                    return result;
                }
                finally
                {
                    path.RemoveAt(path.Count - 1);
                }
            }

            private YamlMappingNode ExpandMapping(YamlMappingNode mapping, List<AnchorFrame> path, AnchorName traversedAnchor, Mark usedAt)
            {
                EnterNode(mapping, path, traversedAnchor);
                try
                {
                    ConsumeBudget(mapping);
                    var result = new YamlMappingNode
                    {
                        Tag = mapping.Tag,
                        Style = mapping.Style,
                    };

                    var explicitKeys = new HashSet<string>(StringComparer.Ordinal);
                    var mergeSources = new List<MergeSource>();
                    foreach (var entry in mapping.Children)
                    {
                        var key = GetScalarKey(entry.Key);
                        if (key.Value == "<<")
                        {
                            CollectMergeSources(entry.Value, mergeSources, key);
                            continue;
                        }

                        if (!explicitKeys.Add(key.Value!))
                        {
                            throw new YamlException(key.Start, key.End, $"Duplicate explicit key '{key.Value}' in mapping. Merge expansion requires each explicit key to be unique.");
                        }
                    }

                    // Merged mappings are expanded in order; earlier sources take precedence.
                    var mergedEntries = new List<KeyValuePair<YamlNode, YamlNode>>();
                    var mergedKeys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var source in mergeSources)
                    {
                        var expandedSource = ExpandMapping(source.Mapping, path, source.Anchor, source.UsedAt);
                        foreach (var sourceEntry in expandedSource.Children)
                        {
                            var sourceKey = (YamlScalarNode)sourceEntry.Key;
                            if (explicitKeys.Contains(sourceKey.Value!) || !mergedKeys.Add(sourceKey.Value!))
                            {
                                // Overridden by an explicit key or by an earlier merge source.
                                // The origin recorded for this value is pruned once the expansion completes.
                                continue;
                            }

                            mergedEntries.Add(sourceEntry);
                        }
                    }

                    foreach (var entry in mapping.Children)
                    {
                        var key = (YamlScalarNode)entry.Key;
                        if (key.Value == "<<")
                        {
                            foreach (var mergedEntry in mergedEntries)
                            {
                                result.Add(mergedEntry.Key, mergedEntry.Value);
                            }

                            continue;
                        }

                        var expandedKey = CopyScalar(key);
                        var expandedValue = ExpandNode(entry.Value, path, entry.Value.Start);
                        result.Add(expandedKey, expandedValue);
                        RecordOrigin(expandedValue, new YamlNodeOrigin(entry.Value.Start, AnchorChainFrom(path), usedAt));
                    }

                    return result;
                }
                finally
                {
                    path.RemoveAt(path.Count - 1);
                }
            }

            private YamlScalarNode CopyScalar(YamlScalarNode scalar)
            {
                ConsumeBudget(scalar);
                return new YamlScalarNode(scalar.Value)
                {
                    Tag = scalar.Tag,
                    Style = scalar.Style,
                };
            }

            private static YamlScalarNode GetScalarKey(YamlNode keyNode)
            {
                if (keyNode is YamlScalarNode scalarKey && scalarKey.Value != null)
                {
                    return scalarKey;
                }

                throw new YamlException(keyNode.Start, keyNode.End, $"Merge expansion supports only scalar string keys, but found a '{keyNode.NodeType}' key.");
            }

            private static void CollectMergeSources(YamlNode value, List<MergeSource> sources, YamlScalarNode mergeKey)
            {
                switch (value)
                {
                    case YamlMappingNode mapping:
                        sources.Add(new MergeSource(mapping, mapping.Anchor, mergeKey.Start));
                        break;

                    case YamlSequenceNode sequence:
                        foreach (var item in sequence.Children)
                        {
                            if (item is YamlMappingNode itemMapping)
                            {
                                sources.Add(new MergeSource(itemMapping, itemMapping.Anchor, mergeKey.Start));
                            }
                            else
                            {
                                throw new YamlException(item.Start, item.End, "Merge sequences may only contain aliases to mappings.");
                            }
                        }

                        break;

                    default:
                        throw new YamlException(value.Start, value.End, "Unrecognized merge key pattern. Expected an alias or a sequence of aliases to mappings.");
                }
            }

            private void RecordOrigin(YamlNode node, YamlNodeOrigin origin)
            {
                origins[node] = origin;
            }

            private void ConsumeBudget(YamlNode node)
            {
                if (++expandedNodeCount > maxExpandedNodes)
                {
                    throw new YamlException(node.Start, node.End, $"The merge expansion exceeded the configured limit of {maxExpandedNodes} nodes. This limit protects against anchor amplification.");
                }
            }

            private static void EnterNode(YamlNode node, List<AnchorFrame> path, AnchorName traversedAnchor)
            {
                foreach (var frame in path)
                {
                    if (ReferenceEquals(frame.Node, node))
                    {
                        throw new YamlException(node.Start, node.End, BuildCycleMessage(path, node, traversedAnchor));
                    }
                }

                path.Add(new AnchorFrame(node, traversedAnchor));
            }

            private static string BuildCycleMessage(List<AnchorFrame> path, YamlNode repeatedNode, AnchorName repeatedAnchor)
            {
                var message = new StringBuilder("Detected a cycle while expanding anchors: ");
                foreach (var frame in path)
                {
                    AppendFrame(message, frame.TraversedAnchor, frame.Node);
                    message.Append(" -> ");
                }

                AppendFrame(message, repeatedAnchor, repeatedNode);
                message.Append(". Shared anchors referenced from distinct locations are allowed, but an anchor cannot contain itself.");
                return message.ToString();
            }

            private static void AppendFrame(StringBuilder message, AnchorName traversedAnchor, YamlNode node)
            {
                var anchor = traversedAnchor.IsEmpty ? node.Anchor : traversedAnchor;
                if (anchor.IsEmpty)
                {
                    message.Append('<').Append(node.NodeType.ToString().ToLowerInvariant()).Append('>');
                }
                else
                {
                    message.Append('\'').Append(anchor).Append('\'');
                }

                message.Append(" (line ").Append(node.Start.Line).Append(", column ").Append(node.Start.Column).Append(')');
            }

            private static AnchorName[] AnchorChainFrom(List<AnchorFrame> path)
            {
                var count = 0;
                foreach (var frame in path)
                {
                    if (!frame.TraversedAnchor.IsEmpty)
                    {
                        count++;
                    }
                }

                var chain = new AnchorName[count];
                var index = 0;
                foreach (var frame in path)
                {
                    if (!frame.TraversedAnchor.IsEmpty)
                    {
                        chain[index++] = frame.TraversedAnchor;
                    }
                }

                return chain;
            }

            /// <summary>
            /// Removes the origins of values that were overridden during the expansion and
            /// therefore did not make it into the final tree, so that every remaining origin
            /// corresponds to a value of the result.
            /// </summary>
            public void PruneOrigins(YamlNode expandedRoot)
            {
                var reachable = new HashSet<YamlNode>(NodeReferenceEqualityComparer.Instance);
                var pending = new Stack<YamlNode>();
                pending.Push(expandedRoot);
                while (pending.Count > 0)
                {
                    var node = pending.Pop();
                    if (!reachable.Add(node))
                    {
                        continue;
                    }

                    if (node is YamlMappingNode mapping)
                    {
                        foreach (var entry in mapping.Children)
                        {
                            pending.Push(entry.Key);
                            pending.Push(entry.Value);
                        }
                    }
                    else if (node is YamlSequenceNode sequence)
                    {
                        foreach (var child in sequence.Children)
                        {
                            pending.Push(child);
                        }
                    }
                }

                var orphaned = new List<YamlNode>();
                foreach (var key in origins.Keys)
                {
                    if (!reachable.Contains(key))
                    {
                        orphaned.Add(key);
                    }
                }

                foreach (var key in orphaned)
                {
                    origins.Remove(key);
                }
            }
        }
    }
}
