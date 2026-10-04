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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization.ValueDeserializers
{
    /// <summary>
    /// Enforces a <see cref="ReferenceBudget" /> while building the object graph.
    /// This deserializer must be registered inside <see cref="AliasValueDeserializer" />
    /// so that aliases are resolved before reaching it: alias events never consume the
    /// constructed-nodes budget, and only nodes that are actually constructed are counted.
    /// The budget state is stored in the <see cref="SerializerState" />, so the limits are
    /// shared by every nested deserialization call of the same document.
    /// </summary>
    public sealed class ReferenceBudgetValueDeserializer : IValueDeserializer
    {
        private readonly IValueDeserializer innerDeserializer;
        private readonly ReferenceBudget budget;

        public ReferenceBudgetValueDeserializer(IValueDeserializer innerDeserializer, ReferenceBudget budget)
        {
            this.innerDeserializer = innerDeserializer ?? throw new ArgumentNullException(nameof(innerDeserializer));
            this.budget = budget ?? throw new ArgumentNullException(nameof(budget));
        }

        public object? DeserializeValue(IParser parser, Type expectedType, SerializerState state, IValueDeserializer nestedObjectDeserializer)
        {
            var tracker = state.Get(() => new ReferenceBudgetTracker(budget));

            // Aliases are consumed by AliasValueDeserializer before this deserializer is invoked,
            // so the current event always corresponds to a node that is about to be constructed.
            var nodeEvent = parser.Current as NodeEvent;
            var start = nodeEvent?.Start ?? Mark.Empty;
            var end = nodeEvent?.End ?? Mark.Empty;

            tracker.TrackConstructedNode(start, end);

            var isAnchoredNode = nodeEvent is { Anchor.IsEmpty: false };
            if (isAnchoredNode)
            {
                tracker.EnterAnchoredNode(start, end);
            }

            try
            {
                return innerDeserializer.DeserializeValue(parser, expectedType, state, nestedObjectDeserializer);
            }
            finally
            {
                if (isAnchoredNode)
                {
                    tracker.ExitAnchoredNode();
                }
            }
        }

        private sealed class ReferenceBudgetTracker
        {
            private readonly ReferenceBudget budget;
            private int constructedNodes;
            private int aliasResolutionDepth;

            public ReferenceBudgetTracker(ReferenceBudget budget)
            {
                this.budget = budget;
            }

            public void TrackConstructedNode(in Mark start, in Mark end)
            {
                if (budget.MaxConstructedNodes is { } maximum && constructedNodes >= maximum)
                {
                    throw new ReferenceBudgetExceededException(start, end, ReferenceBudgetCategory.ConstructedNodes, maximum);
                }

                ++constructedNodes;
            }

            public void EnterAnchoredNode(in Mark start, in Mark end)
            {
                if (budget.MaxAliasResolutionDepth is { } maximum && aliasResolutionDepth >= maximum)
                {
                    throw new ReferenceBudgetExceededException(start, end, ReferenceBudgetCategory.AliasResolutionDepth, maximum);
                }

                ++aliasResolutionDepth;
            }

            public void ExitAnchoredNode()
            {
                --aliasResolutionDepth;
            }
        }
    }
}
