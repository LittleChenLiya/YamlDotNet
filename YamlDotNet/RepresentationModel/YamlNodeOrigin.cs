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
    /// Describes where a node of an expanded tree produced by <see cref="YamlMergeExpander"/>
    /// was originally defined and how it reached its current location.
    /// </summary>
    public sealed class YamlNodeOrigin
    {
        /// <summary>
        /// Gets the position in the input where the value was ultimately defined.
        /// </summary>
        public Mark DefinitionStart { get; }

        /// <summary>
        /// Gets the end position in the input of the ultimate definition.
        /// </summary>
        public Mark DefinitionEnd { get; }

        /// <summary>
        /// Gets the chain of anchors that was traversed to bring the value to its current
        /// location, outermost first. Empty when the value is defined locally.
        /// </summary>
        public IReadOnlyList<AnchorName> AnchorChain { get; }

        /// <summary>
        /// Gets the position in the input where the value is used. For merged values this is
        /// the outermost merge key (&lt;&lt;) that pulled the value in; for local values it
        /// is the same as <see cref="DefinitionStart"/>.
        /// </summary>
        public Mark UseStart { get; }

        /// <summary>
        /// Gets the end position in the input of the use location.
        /// </summary>
        public Mark UseEnd { get; }

        /// <summary>
        /// Gets a value indicating whether the value was defined locally, without going
        /// through a merge.
        /// </summary>
        public bool IsLocal { get; }

        internal YamlNodeOrigin(Mark definitionStart, Mark definitionEnd, IReadOnlyList<AnchorName> anchorChain, Mark useStart, Mark useEnd, bool isLocal)
        {
            DefinitionStart = definitionStart;
            DefinitionEnd = definitionEnd;
            AnchorChain = anchorChain;
            UseStart = useStart;
            UseEnd = useEnd;
            IsLocal = isLocal;
        }

        /// <summary>
        /// Returns a human readable description of this origin.
        /// </summary>
        public override string ToString()
        {
            return IsLocal
                ? $"local value defined at line {DefinitionStart.Line}, column {DefinitionStart.Column}"
                : $"defined at line {DefinitionStart.Line}, column {DefinitionStart.Column}, merged through {string.Join(" -> ", AnchorChain)}, used at line {UseStart.Line}, column {UseStart.Column}";
        }
    }
}
