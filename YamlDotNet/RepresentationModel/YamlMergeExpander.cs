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
using System.Linq;
using System.Runtime.CompilerServices;
using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Provides an opt-in expansion of YAML merge keys (&lt;&lt;) on the representation
    /// model, tracking the origin of every node of the expanded tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Merge keys follow http://yaml.org/type/merge.html: the value of a &lt;&lt; key can be
    /// a single alias to a mapping or a sequence of aliases to mappings. Explicit keys
    /// override merged keys, and mappings that appear earlier in a merge sequence take
    /// priority over later ones.
    /// </para>
    /// <para>
    /// Unlike <see cref="MergingParser"/>, which rewrites the low level event stream during
    /// deserialization and discards all provenance, this API operates on an already loaded
    /// <see cref="YamlDocument"/> or <see cref="YamlNode"/> and returns a new, independent
    /// tree together with the origin of each node. The input tree is never modified and the
    /// regular parsing entry points are unaffected.
    /// </para>
    /// <para>
    /// Only scalar string keys are supported: complex keys and duplicate explicit keys in
    /// the same mapping cause a <see cref="YamlException"/> instead of being silently
    /// stringified. Cycles are detected using the current expansion path, so anchors that
    /// are legitimately shared by several locations are not reported as cycles.
    /// </para>
    /// </remarks>
    public static class YamlMergeExpander
    {
        /// <summary>
        /// Expands the merge keys of the specified document and tracks the origin of every
        /// node of the resulting tree.
        /// </summary>
        public static YamlMergeExpansion ExpandMerges(this YamlDocument document, YamlMergeExpansionOptions? options = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return document.RootNode.ExpandMerges(options);
        }

        /// <summary>
        /// Expands the merge keys of the specified node and tracks the origin of every node
        /// of the resulting tree.
        /// </summary>
        public static YamlMergeExpansion ExpandMerges(this YamlNode root, YamlMergeExpansionOptions? options = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            options ??= new YamlMergeExpansionOptions();
            var context = new ExpansionContext(options.MaxExpandedNodes);
            var expandedRoot = ExpandNode(root, OriginContext.Local, context);
            return new YamlMergeExpansion(expandedRoot, context.Origins);
        }

        private static YamlNode ExpandNode(YamlNode node, OriginContext originContext, ExpansionContext context)
        {
            switch (node)
            {
                case YamlScalarNode scalar:
                    var scalarCopy = new YamlScalarNode(scalar.Value)
                    {
                        Tag = scalar.Tag,
                        Style = scalar.Style,
                    };
                    context.RegisterNode(scalarCopy, scalar, originContext);
                    return scalarCopy;

                case YamlSequenceNode sequence:
                    return ExpandSequence(sequence, originContext, context);

                case YamlMappingNode mapping:
                    return ExpandMapping(mapping, originContext, context);

                case YamlAliasNode alias:
                    throw new YamlException(alias.Start, alias.End, $"Cannot expand merges because the alias '{alias}' could not be resolved.");

                default:
                    throw new YamlException(node.Start, node.End, $"Cannot expand merges for nodes of type '{node.NodeType}'.");
            }
        }

        private static YamlSequenceNode ExpandSequence(YamlSequenceNode sequence, OriginContext originContext, ExpansionContext context)
        {
            context.Enter(sequence, originContext);
            try
            {
                var copy = new YamlSequenceNode
                {
                    Tag = sequence.Tag,
                    Style = sequence.Style,
                };
                context.RegisterNode(copy, sequence, originContext);

                foreach (var child in sequence.Children)
                {
                    copy.Children.Add(ExpandNode(child, originContext, context));
                }

                return copy;
            }
            finally
            {
                context.Exit();
            }
        }

        private static YamlMappingNode ExpandMapping(YamlMappingNode mapping, OriginContext originContext, ExpansionContext context)
        {
            context.Enter(mapping, originContext);
            try
            {
                var copy = new YamlMappingNode
                {
                    Tag = mapping.Tag,
                    Style = mapping.Style,
                };
                context.RegisterNode(copy, mapping, originContext);

                foreach (var entry in ExpandMappingEntries(mapping, originContext, context, null))
                {
                    copy.Children.Add(entry.Key, entry.Value);
                }

                return copy;
            }
            finally
            {
                context.Exit();
            }
        }

        private static List<ExpandedEntry> ExpandMappingEntries(YamlMappingNode mapping, OriginContext originContext, ExpansionContext context, Func<string, bool>? isSuppressed)
        {
            var explicitKeys = new HashSet<string>(StringComparer.Ordinal);
            var explicitEntries = new List<(string Name, YamlScalarNode Key, YamlNode Value)>();
            var mergeEntries = new List<KeyValuePair<YamlNode, YamlNode>>();

            foreach (var child in mapping.Children)
            {
                if (child.Key is not YamlScalarNode keyScalar)
                {
                    throw new YamlException(child.Key.Start, child.Key.End, "Merge expansion supports only scalar string keys. Complex mapping keys are not supported; convert them explicitly instead of relying on key stringification.");
                }

                var keyName = keyScalar.Value ?? string.Empty;
                if (keyName == "<<")
                {
                    mergeEntries.Add(child);
                    continue;
                }

                if (!explicitKeys.Add(keyName))
                {
                    throw new YamlException(child.Key.Start, child.Key.End, $"Duplicate explicit key '{keyName}' in mapping. Merge expansion requires explicit keys to be unique.");
                }

                explicitEntries.Add((keyName, keyScalar, child.Value));
            }

            var result = new List<ExpandedEntry>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var mergeEntry in mergeEntries)
            {
                foreach (var source in ResolveMergeSources(mergeEntry))
                {
                    var sourceContext = originContext.Extend(source.Anchor, mergeEntry.Key.Start, mergeEntry.Key.End);

                    List<ExpandedEntry> sourceEntries;
                    context.Enter(source, sourceContext);
                    try
                    {
                        // Keys that are overridden locally or were already provided by an
                        // earlier source are suppressed before expansion so that overridden
                        // values never create nodes nor origins.
                        sourceEntries = ExpandMappingEntries(source, sourceContext, context, ShouldSuppress);
                    }
                    finally
                    {
                        context.Exit();
                    }

                    foreach (var entry in sourceEntries)
                    {
                        if (!explicitKeys.Contains(entry.Name) && emitted.Add(entry.Name))
                        {
                            result.Add(entry);
                        }
                    }
                }
            }

            foreach (var (name, key, value) in explicitEntries)
            {
                if (isSuppressed != null && isSuppressed(name))
                {
                    continue;
                }

                emitted.Add(name);
                result.Add(new ExpandedEntry(
                    name,
                    ExpandNode(key, originContext, context),
                    ExpandNode(value, originContext, context)));
            }

            return result;

            bool ShouldSuppress(string name)
            {
                return explicitKeys.Contains(name) || emitted.Contains(name) || (isSuppressed != null && isSuppressed(name));
            }
        }

        private static IEnumerable<YamlMappingNode> ResolveMergeSources(KeyValuePair<YamlNode, YamlNode> mergeEntry)
        {
            if (mergeEntry.Value is YamlMappingNode singleMapping)
            {
                yield return singleMapping;
                yield break;
            }

            if (mergeEntry.Value is YamlSequenceNode sequence)
            {
                foreach (var item in sequence.Children)
                {
                    if (item is YamlMappingNode itemMapping)
                    {
                        yield return itemMapping;
                    }
                    else
                    {
                        throw new YamlException(item.Start, item.End, "Invalid merge value: the entries of a merge sequence must be aliases to mappings.");
                    }
                }

                yield break;
            }

            throw new YamlException(mergeEntry.Key.Start, mergeEntry.Value.End, "Unrecognized merge key pattern: the value of '<<' must be an alias to a mapping or a sequence of aliases to mappings.");
        }

        private readonly struct ExpandedEntry
        {
            public string Name { get; }
            public YamlNode Key { get; }
            public YamlNode Value { get; }

            public ExpandedEntry(string name, YamlNode key, YamlNode value)
            {
                Name = name;
                Key = key;
                Value = value;
            }
        }

        private sealed class OriginContext
        {
            public static readonly OriginContext Local = new OriginContext(true, [], Mark.Empty, Mark.Empty);

            public bool IsLocal { get; }
            public IReadOnlyList<AnchorName> Chain { get; }
            public Mark UseStart { get; }
            public Mark UseEnd { get; }

            private OriginContext(bool isLocal, IReadOnlyList<AnchorName> chain, Mark useStart, Mark useEnd)
            {
                IsLocal = isLocal;
                Chain = chain;
                UseStart = useStart;
                UseEnd = useEnd;
            }

            public OriginContext Extend(AnchorName anchor, Mark useStart, Mark useEnd)
            {
                var chain = Chain;
                if (!anchor.IsEmpty)
                {
                    var extendedChain = new AnchorName[Chain.Count + 1];
                    for (var i = 0; i < Chain.Count; i++)
                    {
                        extendedChain[i] = Chain[i];
                    }

                    extendedChain[Chain.Count] = anchor;
                    chain = extendedChain;
                }

                // The use location of the outermost merge is preserved so that the origin
                // points at the place where the value is ultimately consumed.
                return IsLocal
                    ? new OriginContext(false, chain, useStart, useEnd)
                    : new OriginContext(false, chain, UseStart, UseEnd);
            }
        }

        private sealed class ExpansionContext
        {
            private readonly List<YamlNode> pathNodes = [];
            private readonly List<string> pathNames = [];
            private readonly int maxExpandedNodes;
            private int expandedNodeCount;

            public Dictionary<YamlNode, YamlNodeOrigin> Origins { get; } = new Dictionary<YamlNode, YamlNodeOrigin>(NodeIdentityComparer.Instance);

            public ExpansionContext(int maxExpandedNodes)
            {
                this.maxExpandedNodes = maxExpandedNodes;
            }

            public void RegisterNode(YamlNode result, YamlNode source, OriginContext originContext)
            {
                expandedNodeCount++;
                if (expandedNodeCount > maxExpandedNodes)
                {
                    throw new YamlException(source.Start, source.End, $"Too many expanded nodes. The configured limit of {maxExpandedNodes} was exceeded to prevent excessive memory usage caused by anchor amplification.");
                }

                var origin = originContext.IsLocal
                    ? new YamlNodeOrigin(source.Start, source.End, [], source.Start, source.End, isLocal: true)
                    : new YamlNodeOrigin(source.Start, source.End, originContext.Chain, originContext.UseStart, originContext.UseEnd, isLocal: false);
                Origins.Add(result, origin);
            }

            public void Enter(YamlNode node, OriginContext originContext)
            {
                for (var i = 0; i < pathNodes.Count; i++)
                {
                    if (ReferenceEquals(pathNodes[i], node))
                    {
                        var chain = string.Join(" -> ", pathNames.Skip(i)) + " -> " + NameOf(node);
                        var start = originContext.IsLocal ? node.Start : originContext.UseStart;
                        var end = originContext.IsLocal ? node.End : originContext.UseEnd;
                        throw new YamlException(start, end, $"Detected a cycle during merge expansion: {chain} (at line {start.Line}, column {start.Column}). Anchors shared by distinct locations are allowed; this chain revisits a node that is still being expanded.");
                    }
                }

                pathNodes.Add(node);
                pathNames.Add(NameOf(node));
            }

            public void Exit()
            {
                pathNodes.RemoveAt(pathNodes.Count - 1);
                pathNames.RemoveAt(pathNames.Count - 1);
            }

            private static string NameOf(YamlNode node)
            {
                if (!node.Anchor.IsEmpty)
                {
                    return $"'{node.Anchor.Value}'";
                }

                return node is YamlMappingNode ? "<mapping>" : "<sequence>";
            }
        }

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
                // The default GetHashCode of container nodes is value based and changes as
                // children are added, so the identity hash code is required here.
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
