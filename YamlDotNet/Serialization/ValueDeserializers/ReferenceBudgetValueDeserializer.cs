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
    /// Enforces a <see cref="ReferenceBudget"/> while deserializing. This deserializer is placed
    /// outside of the <see cref="AliasValueDeserializer"/> so that it observes every node that is
    /// constructed and every alias that is resolved. The budget is stored in the
    /// <see cref="SerializerState"/>, which means that it is shared by all nested deserialization
    /// calls for the same operation.
    /// </summary>
    public sealed class ReferenceBudgetValueDeserializer : IValueDeserializer
    {
        private readonly IValueDeserializer innerDeserializer;
        private readonly int? maximumNodes;
        private readonly int? maximumAliasDepth;

        public ReferenceBudgetValueDeserializer(IValueDeserializer innerDeserializer, int? maximumNodes, int? maximumAliasDepth)
        {
            this.innerDeserializer = innerDeserializer ?? throw new ArgumentNullException(nameof(innerDeserializer));
            this.maximumNodes = maximumNodes;
            this.maximumAliasDepth = maximumAliasDepth;
        }

        public object? DeserializeValue(IParser parser, Type expectedType, SerializerState state, IValueDeserializer nestedObjectDeserializer)
        {
            var budget = state.Get(() => new ReferenceBudget(maximumNodes, maximumAliasDepth));

            if (parser.Accept<AnchorAlias>(out var alias))
            {
                // An alias does not construct a new node: it reuses an already constructed object
                // (or a promise to one). Track the depth of aliases that are currently being resolved.
                budget.EnterAlias(alias.Start, alias.End);

                var result = innerDeserializer.DeserializeValue(parser, expectedType, state, nestedObjectDeserializer);

                if (result is IValuePromise promise)
                {
                    // The alias resolves to an object that is still being constructed (self or forward
                    // reference). The alias is "being resolved" until the promise is fulfilled.
                    promise.ValueAvailable += _ => budget.LeaveAlias();
                }
                else
                {
                    // The alias resolved to an already constructed object: its resolution is complete.
                    budget.LeaveAlias();
                }

                return result;
            }

            // Every other event constructs exactly one node. Aliases are handled above and must not
            // be counted, so shared objects are not mistakenly counted as multiple constructions.
            var nodeEvent = parser.Current;
            budget.EnterNode(nodeEvent?.Start ?? Mark.Empty, nodeEvent?.End ?? Mark.Empty);

            return innerDeserializer.DeserializeValue(parser, expectedType, state, nestedObjectDeserializer);
        }
    }
}
