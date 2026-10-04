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

using System.Collections;
using System.IO;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace YamlDotNet.Test.Serialization
{
    public class ReferenceBudgetTests
    {
        [Fact]
        public void SharedAlias_PreservesObjectIdentity()
        {
            // A sequence that contains the same anchored mapping twice via an alias.
            var yaml = "- &x { a: 1 }\n- *x";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 100, maximumAliasDepth: 100)
                .Build();

            var list = sut.Deserialize<IList>(new StringReader(yaml));

            Assert.Equal(2, list.Count);
            Assert.Same(list[0], list[1]);
        }

        [Fact]
        public void AliasesSharingObject_AreNotCountedAsConstructions()
        {
            // Only four nodes are constructed: the root sequence, the mapping and its two scalars.
            // The three aliases reuse the already constructed mapping and must not be counted.
            var yaml = "- &x { a: 1 }\n- *x\n- *x\n- *x";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 4, maximumAliasDepth: 100)
                .Build();

            var list = sut.Deserialize<IList>(new StringReader(yaml));

            Assert.Equal(4, list.Count);
            Assert.Same(list[0], list[1]);
        }

        [Fact]
        public void ThreeLayerAliasChain_WithDepthTwo_ThrowsAtThirdLayer()
        {
            // Three nested anchors a, b and c, each aliased from within the innermost sequence.
            // While the aliases are resolved the alias depth grows to three, so a depth budget of
            // two must reject the third alias.
            var yaml = "- &a\n  - &b\n    - &c\n      - *a\n      - *b\n      - *c";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 100, maximumAliasDepth: 2)
                .Build();

            var exception = Assert.Throws<ReferenceBudgetExceededException>(
                () => sut.Deserialize<IList>(new StringReader(yaml)));

            Assert.Equal(ReferenceBudgetCategory.AliasDepth, exception.Category);
            Assert.Equal(6, exception.Start.Line);
            Assert.Equal(9, exception.Start.Column);
        }

        [Fact]
        public void AfterBudgetExceeded_SameDeserializer_ParsesNormalInput()
        {
            var chainYaml = "- &a\n  - &b\n    - &c\n      - *a\n      - *b\n      - *c";
            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 100, maximumAliasDepth: 2)
                .Build();

            Assert.Throws<ReferenceBudgetExceededException>(
                () => sut.Deserialize<IList>(new StringReader(chainYaml)));

            // The same deserializer instance must still be usable: the budget is per operation.
            var normal = sut.Deserialize<IList>(new StringReader("- 1\n- 2\n- 3"));
            Assert.Equal(3, normal.Count);
        }

        [Fact]
        public void SelfReference_WithBudget_IsAllowed()
        {
            // A self-referential mapping: the alias points at the mapping that is still being
            // constructed. This is a legitimate alias and must not be rejected.
            var yaml = "&a { x: *a }";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 100, maximumAliasDepth: 10)
                .Build();

            var dictionary = sut.Deserialize<IDictionary>(new StringReader(yaml));

            Assert.Same(dictionary["x"], dictionary);
        }

        [Fact]
        public void NodeBudget_AtExactLimit_Succeeds()
        {
            // "[1, 2, 3]" constructs four nodes: one sequence and three scalars.
            var yaml = "[1, 2, 3]";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 4, maximumAliasDepth: 100)
                .Build();

            var list = sut.Deserialize<IList>(new StringReader(yaml));

            Assert.Equal(3, list.Count);
        }

        [Fact]
        public void NodeBudget_OneBelowLimit_Throws()
        {
            var yaml = "[1, 2, 3]";

            var sut = new DeserializerBuilder()
                .WithReferenceBudget(maximumNodes: 3, maximumAliasDepth: 100)
                .Build();

            var exception = Assert.Throws<ReferenceBudgetExceededException>(
                () => sut.Deserialize<IList>(new StringReader(yaml)));

            Assert.Equal(ReferenceBudgetCategory.NodeCount, exception.Category);
        }

        [Fact]
        public void Default_WithoutBudget_AllowsObjectGraphWithAliases()
        {
            // By default the budget is disabled and must not change the behavior of the deserializer.
            var yaml = "- &x { a: 1 }\n- *x\n- *x";

            var sut = new DeserializerBuilder().Build();

            var list = sut.Deserialize<IList>(new StringReader(yaml));

            Assert.Equal(3, list.Count);
            Assert.Same(list[0], list[1]);
        }

        [Fact]
        public void WithReferenceBudget_InvalidLimits_Throw()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new DeserializerBuilder().WithReferenceBudget(maximumNodes: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new DeserializerBuilder().WithReferenceBudget(maximumAliasDepth: -1));
        }
    }
}
