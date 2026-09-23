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
using System.Text;
using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Describes where a value produced by <see cref="YamlMergeExpander"/> was originally defined.
    /// </summary>
    public sealed class YamlNodeOrigin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YamlNodeOrigin"/> class.
        /// </summary>
        /// <param name="definedAt">The position where the value that was ultimately used is defined.</param>
        /// <param name="anchorChain">The anchors that were traversed to reach the definition, from the use site towards the definition.</param>
        /// <param name="usedAt">The position where the value is used (the merge key that pulled it in, or its own location for local values).</param>
        public YamlNodeOrigin(Mark definedAt, IReadOnlyList<AnchorName> anchorChain, Mark usedAt)
        {
            DefinedAt = definedAt;
            AnchorChain = anchorChain;
            UsedAt = usedAt;
        }

        /// <summary>
        /// Gets the position where the value that was ultimately used is defined.
        /// </summary>
        public Mark DefinedAt { get; }

        /// <summary>
        /// Gets the anchors that were traversed to reach the definition,
        /// ordered from the use site towards the definition.
        /// Empty when the value was defined locally, without going through any anchor.
        /// </summary>
        public IReadOnlyList<AnchorName> AnchorChain { get; }

        /// <summary>
        /// Gets the position where the value is used. For values pulled in through a merge key,
        /// this is the position of the <c>&lt;&lt;</c> key; for local values, this is the position
        /// of the mapping that directly contains them.
        /// </summary>
        public Mark UsedAt { get; }

        /// <summary>
        /// Gets a value indicating whether the value was defined locally, without traversing any anchor.
        /// </summary>
        public bool IsLocal => AnchorChain.Count == 0;

        /// <summary>
        /// Returns a <see cref="string"/> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("defined at line ").Append(DefinedAt.Line).Append(", column ").Append(DefinedAt.Column);
            if (AnchorChain.Count > 0)
            {
                text.Append(" via ");
                for (var i = 0; i < AnchorChain.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(" -> ");
                    }

                    text.Append('\u0027').Append(AnchorChain[i]).Append('\u0027');
                }
            }

            text.Append(", used at line ").Append(UsedAt.Line).Append(", column ").Append(UsedAt.Column);
            return text.ToString();
        }
    }
}
