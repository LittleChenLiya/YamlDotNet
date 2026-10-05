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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Represents the result of projecting a <see cref="YamlNode"/> graph into a self-contained tree
    /// by <see cref="YamlTreeProjection"/>.
    /// </summary>
    public sealed class YamlTreeProjectionResult
    {
        /// <summary>
        /// Gets the root of the source graph that was projected. The source graph is never modified.
        /// </summary>
        public YamlNode SourceRoot { get; }

        /// <summary>
        /// Gets the root of the projected tree. Every node in this tree is a new instance;
        /// no node is shared, and no node carries an anchor or is an alias.
        /// </summary>
        public YamlNode Root { get; }

        /// <summary>
        /// Gets the exact number of nodes in the projected tree.
        /// </summary>
        public long NodeCount { get; }

        /// <summary>
        /// Gets the provenance of the projection: for every node of the projected tree (the key,
        /// compared by reference), the node of the source graph it was copied from (the value).
        /// Copies that originate from the same shared source node all map back to that same
        /// source node instance.
        /// </summary>
        public IReadOnlyDictionary<YamlNode, YamlNode> Provenance { get; }

        internal YamlTreeProjectionResult(YamlNode sourceRoot, YamlNode root, long nodeCount, IReadOnlyDictionary<YamlNode, YamlNode> provenance)
        {
            SourceRoot = sourceRoot;
            Root = root;
            NodeCount = nodeCount;
            Provenance = provenance;
        }

        /// <summary>
        /// Gets the node of the source graph that <paramref name="projectedNode"/> was copied from,
        /// or <see langword="null"/> if <paramref name="projectedNode"/> is not part of this projection.
        /// </summary>
        /// <param name="projectedNode">A node of the projected tree.</param>
        /// <returns>The original node, or <see langword="null"/> when the node is unknown to this projection.</returns>
        public YamlNode? GetOriginal(YamlNode projectedNode)
        {
            return Provenance.TryGetValue(projectedNode, out var original) ? original : null;
        }
    }
}
