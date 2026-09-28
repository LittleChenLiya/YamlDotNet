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

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// The anchor assignments computed for a single document in <see cref="AnchorNamingMode.Deterministic"/> mode.
    /// The graph of <see cref="YamlNode"/> instances is never modified.
    /// </summary>
    internal sealed class DeterministicAnchorPlan
    {
        public Dictionary<YamlNode, AnchorName> Anchors { get; }

        public DeterministicAnchorPlan(Dictionary<YamlNode, AnchorName> anchors)
        {
            Anchors = anchors;
        }
    }

    /// <summary>
    /// Analyzes a document graph completely before anything is emitted and computes the anchors
    /// needed to represent node sharing and cycles without assigning names to nodes that are
    /// merely equal but not identical.
    /// </summary>
    internal static class DeterministicAnchorPlanner
    {
        /// <summary>
        /// Performs a depth-first traversal of <paramref name="root"/> in emission order and
        /// returns the anchors that must be used when emitting the document. All validation is
        /// performed here so that invalid graphs fail before any byte is written.
        /// </summary>
        public static DeterministicAnchorPlan Analyze(YamlNode root)
        {
            if (root is YamlAliasNode)
            {
                throw new YamlException("A document cannot contain only an alias.");
            }

            var referenceComparer = NodeReferenceComparer.Instance;

            // Position of the first appearance of every node in a depth-first, emission-order walk.
            var positions = new Dictionary<YamlNode, int>(referenceComparer);

            // Number of edges that point at every node. A count greater than one means that the
            // node is shared (or is the target of a cycle edge) and therefore needs an anchor.
            var incomingReferences = new Dictionary<YamlNode, int>(referenceComparer);

            var firstAppearanceOrder = new List<YamlNode>();
            var aliasReferences = new List<KeyValuePair<AnchorName, int>>();

            Traverse(root, positions, incomingReferences, firstAppearanceOrder, aliasReferences);

            var anchors = new Dictionary<YamlNode, AnchorName>(referenceComparer);
            var anchorPositions = new Dictionary<AnchorName, int>();
            var usedAnchorNames = new HashSet<AnchorName>();

            // Existing explicit anchors are kept, in first appearance order. Duplicate names are
            // a conflict and must be reported before the graph is written.
            foreach (var node in firstAppearanceOrder)
            {
                if (node.Anchor.IsEmpty)
                {
                    continue;
                }

                if (!usedAnchorNames.Add(node.Anchor))
                {
                    throw new YamlException(string.Format(
                        CultureInfo.InvariantCulture,
                        "The anchor '{0}' is assigned to more than one node in the document.",
                        node.Anchor.Value));
                }

                anchors.Add(node, node.Anchor);
                anchorPositions.Add(node.Anchor, positions[node]);
            }

            // Assign a1, a2, ... to shared nodes that do not already have an explicit name, in
            // first appearance order. A collision with a reserved explicit name is a conflict.
            var nextAutoIndex = 1;
            foreach (var node in firstAppearanceOrder)
            {
                if (!node.Anchor.IsEmpty || incomingReferences[node] < 2)
                {
                    continue;
                }

                var candidate = new AnchorName("a" + nextAutoIndex.ToString(CultureInfo.InvariantCulture));
                nextAutoIndex++;

                if (!usedAnchorNames.Add(candidate))
                {
                    throw new YamlException(string.Format(
                        CultureInfo.InvariantCulture,
                        "The deterministic anchor name '{0}' conflicts with an existing explicit anchor.",
                        candidate.Value));
                }

                anchors.Add(node, candidate);
                anchorPositions.Add(candidate, positions[node]);
            }

            // Aliases can only point at a previously emitted anchor; forward and dangling aliases
            // are not representable.
            foreach (var aliasReference in aliasReferences)
            {
                var aliasName = aliasReference.Key;
                var aliasPosition = aliasReference.Value;

                if (!anchorPositions.TryGetValue(aliasName, out var anchorPosition))
                {
                    throw new YamlException(string.Format(
                        CultureInfo.InvariantCulture,
                        "The alias '{0}' does not reference a node that has an anchor.",
                        aliasName.Value));
                }

                if (anchorPosition > aliasPosition)
                {
                    throw new YamlException(string.Format(
                        CultureInfo.InvariantCulture,
                        "The alias '{0}' appears before the node that defines its anchor; forward aliases cannot be emitted.",
                        aliasName.Value));
                }
            }

            return new DeterministicAnchorPlan(anchors);
        }

        private static void Traverse(
            YamlNode root,
            Dictionary<YamlNode, int> positions,
            Dictionary<YamlNode, int> incomingReferences,
            List<YamlNode> firstAppearanceOrder,
            List<KeyValuePair<AnchorName, int>> aliasReferences)
        {
            var frames = new Stack<TraversalFrame>();

            positions.Add(root, 0);
            incomingReferences.Add(root, 1);
            firstAppearanceOrder.Add(root);
            frames.Push(new TraversalFrame(root, EnumerateChildren(root).GetEnumerator()));

            var nextPosition = 1;

            while (frames.Count > 0)
            {
                var frame = frames.Peek();
                if (!frame.Children.MoveNext())
                {
                    frame.Children.Dispose();
                    frames.Pop();
                    continue;
                }

                var child = frame.Children.Current;

                if (child is YamlAliasNode aliasNode)
                {
                    aliasReferences.Add(new KeyValuePair<AnchorName, int>(aliasNode.Anchor, nextPosition));
                    nextPosition++;
                    continue;
                }

                if (positions.TryGetValue(child, out _))
                {
                    incomingReferences[child]++;
                }
                else
                {
                    positions.Add(child, nextPosition);
                    incomingReferences.Add(child, 1);
                    firstAppearanceOrder.Add(child);
                    nextPosition++;
                    frames.Push(new TraversalFrame(child, EnumerateChildren(child).GetEnumerator()));
                }
            }
        }

        private static IEnumerable<YamlNode> EnumerateChildren(YamlNode node)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var entry in mapping.Children)
                    {
                        yield return entry.Key;
                        yield return entry.Value;
                    }

                    break;

                case YamlSequenceNode sequence:
                    foreach (var child in sequence.Children)
                    {
                        yield return child;
                    }

                    break;
            }
        }

        private sealed class TraversalFrame
        {
            public TraversalFrame(YamlNode node, IEnumerator<YamlNode> children)
            {
                Node = node;
                Children = children;
            }

            public YamlNode Node { get; }

            public IEnumerator<YamlNode> Children { get; }
        }
    }

    /// <summary>
    /// Compares nodes by reference identity, regardless of their value-based <see cref="object.GetHashCode"/> implementation.
    /// </summary>
    internal sealed class NodeReferenceComparer : IEqualityComparer<YamlNode>
    {
        public static readonly NodeReferenceComparer Instance = new NodeReferenceComparer();

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
