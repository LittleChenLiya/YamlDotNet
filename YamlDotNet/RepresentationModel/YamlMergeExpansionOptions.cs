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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Configures the behavior of <see cref="YamlMergeExpander"/>.
    /// </summary>
    public sealed class YamlMergeExpansionOptions
    {
        /// <summary>
        /// The default upper bound of nodes created by a single merge expansion.
        /// </summary>
        public const int DefaultMaxExpandedNodes = 100_000;

        private int maxExpandedNodes = DefaultMaxExpandedNodes;

        /// <summary>
        /// Gets or sets the maximum number of nodes that a single merge expansion is allowed to create.
        /// This protects against anchor amplification, where a small document expands to an
        /// unreasonably large tree. Must be a positive integer.
        /// </summary>
        public int MaxExpandedNodes
        {
            get => maxExpandedNodes;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxExpandedNodes must be a positive integer.");
                }

                maxExpandedNodes = value;
            }
        }
    }
}
