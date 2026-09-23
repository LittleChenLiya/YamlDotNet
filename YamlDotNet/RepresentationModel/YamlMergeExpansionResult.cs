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
    /// Represents the result of expanding the merge keys of a YAML node tree
    /// with <see cref="YamlMergeExpander"/>.
    /// </summary>
    public sealed class YamlMergeExpansionResult
    {
        private readonly IReadOnlyDictionary<YamlNode, YamlNodeOrigin> origins;

        internal YamlMergeExpansionResult(YamlNode root, IReadOnlyDictionary<YamlNode, YamlNodeOrigin> origins)
        {
            Root = root;
            this.origins = origins;
        }

        /// <summary>
        /// Gets the root of the expanded node tree. This tree is independent from the
        /// original tree: modifying it does not affect the original nodes, and each
        /// expansion branch owns its own containers.
        /// </summary>
        public YamlNode Root { get; }

        /// <summary>
        /// Gets the origin of each value of the expanded mappings, keyed by the
        /// corresponding node of the expanded tree. Every value that appears in an
        /// expanded mapping has exactly one origin, and every origin corresponds to
        /// a value of the expanded tree.
        /// </summary>
        public IReadOnlyDictionary<YamlNode, YamlNodeOrigin> Origins => origins;

        /// <summary>
        /// Gets the origin of the specified node of the expanded tree, or <see langword="null"/>
        /// when the node has no origin information (for example sequence items or keys).
        /// </summary>
        public YamlNodeOrigin? GetOrigin(YamlNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return origins.TryGetValue(node, out var origin) ? origin : null;
        }
    }
}
