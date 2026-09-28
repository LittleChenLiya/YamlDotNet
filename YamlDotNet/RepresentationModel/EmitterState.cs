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
using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Holds state that is used when emitting a stream.
    /// </summary>
    internal class EmitterState
    {
        public EmitterState()
        {
        }

        public EmitterState(DeterministicAnchorPlan deterministicAnchors)
        {
            DeterministicAnchors = deterministicAnchors;
        }

        /// <summary>
        /// Gets the already emitted anchors.
        /// </summary>
        /// <value>The emitted anchors.</value>
        public HashSet<AnchorName> EmittedAnchors { get; } = [];

        /// <summary>
        /// Gets the nodes that have already been emitted as entity nodes when deterministic anchors are used.
        /// </summary>
        public HashSet<YamlNode> EmittedNodes { get; } = new HashSet<YamlNode>(NodeReferenceComparer.Instance);

        /// <summary>
        /// Gets the precomputed anchor assignments, or <c>null</c> when anchors come from the nodes themselves.
        /// </summary>
        public DeterministicAnchorPlan? DeterministicAnchors { get; }
    }
}
