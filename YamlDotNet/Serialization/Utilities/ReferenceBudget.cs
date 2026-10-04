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

using YamlDotNet.Core;

namespace YamlDotNet.Serialization.Utilities
{
    /// <summary>
    /// Keeps track of the reference budgets for a single deserialization operation.
    /// The node count is a cumulative total of the nodes that were actually constructed,
    /// while the alias depth tracks how many aliases are currently being resolved.
    /// Instances are stored in the <see cref="SerializerState"/> so that the limits are
    /// shared by every nested deserialization call.
    /// </summary>
    internal sealed class ReferenceBudget
    {
        private int nodeCount;
        private int aliasDepth;

        /// <summary>
        /// Gets the maximum number of nodes that may be constructed.
        /// A <see langword="null"/> value means that there is no limit.
        /// </summary>
        public int? MaximumNodes { get; }

        /// <summary>
        /// Gets the maximum depth of aliases that may be resolved simultaneously.
        /// A <see langword="null"/> value means that there is no limit.
        /// </summary>
        public int? MaximumAliasDepth { get; }

        public ReferenceBudget(int? maximumNodes, int? maximumAliasDepth)
        {
            MaximumNodes = maximumNodes;
            MaximumAliasDepth = maximumAliasDepth;
        }

        /// <summary>
        /// Registers the construction of a new node. Aliases do not construct nodes and
        /// must not be counted. Throws <see cref="ReferenceBudgetExceededException"/> if
        /// the node budget has been reached.
        /// </summary>
        public void EnterNode(in Mark start, in Mark end)
        {
            if (MaximumNodes is { } maximum && nodeCount >= maximum)
            {
                throw new ReferenceBudgetExceededException(
                    ReferenceBudgetCategory.NodeCount,
                    start,
                    end,
                    $"Reference budget exceeded: the maximum number of constructed nodes ({maximum}) has been reached.");
            }

            ++nodeCount;
        }

        /// <summary>
        /// Registers the start of an alias resolution. Throws <see cref="ReferenceBudgetExceededException"/>
        /// if the alias depth budget has been reached.
        /// </summary>
        public void EnterAlias(in Mark start, in Mark end)
        {
            if (MaximumAliasDepth is { } maximum && aliasDepth >= maximum)
            {
                throw new ReferenceBudgetExceededException(
                    ReferenceBudgetCategory.AliasDepth,
                    start,
                    end,
                    $"Reference budget exceeded: the maximum alias depth ({maximum}) has been reached.");
            }

            ++aliasDepth;
        }

        /// <summary>
        /// Registers the completion of an alias resolution.
        /// </summary>
        public void LeaveAlias()
        {
            --aliasDepth;
        }
    }
}
