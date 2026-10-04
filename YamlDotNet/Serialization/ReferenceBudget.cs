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

namespace YamlDotNet.Serialization
{
    /// <summary>
    /// Optional limits that guard deserialization against small YAML documents that expand
    /// into very large object graphs through anchors and aliases.
    /// </summary>
    /// <remarks>
    /// The budget is disabled by default. Enable it through
    /// <see cref="DeserializerBuilder.WithReferenceBudget(ReferenceBudget)" /> (or the equivalent
    /// method on <see cref="StaticDeserializerBuilder" />) when parsing untrusted input.
    /// <para>
    /// Two independent limits can be configured; either of them may be omitted:
    /// <list type="bullet">
    /// <item>
    /// <see cref="MaxConstructedNodes" /> caps the number of YAML nodes that are actually
    /// constructed into objects. An alias that refers to an already anchored node does not
    /// construct a new node and therefore does not consume this budget.
    /// </item>
    /// <item>
    /// <see cref="MaxAliasResolutionDepth" /> caps how many anchored nodes may be in the
    /// process of being resolved at the same time (i.e. the nesting depth of anchored nodes).
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// When a limit is exceeded, a <see cref="YamlDotNet.Core.ReferenceBudgetExceededException" />
    /// is thrown. The exception carries the position in the YAML document and the
    /// <see cref="YamlDotNet.Core.ReferenceBudgetCategory" /> of the exceeded budget,
    /// and no partially constructed object graph is returned to the caller.
    /// </para>
    /// </remarks>
    public sealed class ReferenceBudget
    {
        /// <summary>
        /// Gets the maximum number of YAML nodes that may actually be constructed during
        /// deserialization, or <c>null</c> when the number of constructed nodes is not limited.
        /// Aliases to previously anchored nodes are not counted.
        /// </summary>
        public int? MaxConstructedNodes { get; }

        /// <summary>
        /// Gets the maximum number of anchored nodes that may be in the process of being
        /// resolved at the same time, or <c>null</c> when the alias resolution depth is not limited.
        /// </summary>
        public int? MaxAliasResolutionDepth { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ReferenceBudget" />.
        /// At least one of the limits must be specified.
        /// </summary>
        /// <param name="maxConstructedNodes">
        /// The maximum number of nodes that may actually be constructed. Must be a positive integer when specified.
        /// </param>
        /// <param name="maxAliasResolutionDepth">
        /// The maximum nesting depth of anchored nodes that are being resolved. Must be a positive integer when specified.
        /// </param>
        public ReferenceBudget(int? maxConstructedNodes = null, int? maxAliasResolutionDepth = null)
        {
            if (maxConstructedNodes is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConstructedNodes), maxConstructedNodes, "The maximum number of constructed nodes must be a positive integer.");
            }

            if (maxAliasResolutionDepth is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAliasResolutionDepth), maxAliasResolutionDepth, "The maximum alias resolution depth must be a positive integer.");
            }

            if (maxConstructedNodes == null && maxAliasResolutionDepth == null)
            {
                throw new ArgumentException("At least one budget limit must be specified.", nameof(maxConstructedNodes));
            }

            MaxConstructedNodes = maxConstructedNodes;
            MaxAliasResolutionDepth = maxAliasResolutionDepth;
        }
    }
}
