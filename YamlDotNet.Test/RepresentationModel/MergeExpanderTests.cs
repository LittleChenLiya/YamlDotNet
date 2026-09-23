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
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class MergeExpanderTests
    {
        [Fact]
        public void MergeSequenceWithExplicitOverrideProducesExpectedValuesAndOrigins()
        {
            var yaml = Load(@"
base: &base
  x: 1
  y: 2
next: &next
  x: 3
merged:
  <<: [*base, *next]
  y: 9
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var inputMerged = (YamlMappingNode)root.Children[new YamlScalarNode("merged")];
            var inputBase = (YamlMappingNode)root.Children[new YamlScalarNode("base")];
            var mergeKeyNode = inputMerged.Children.Keys.Cast<YamlScalarNode>().Single(k => k.Value == "<<");
            var baseXValue = inputBase.Children[new YamlScalarNode("x")];
            var localYValue = inputMerged.Children[new YamlScalarNode("y")];

            var result = YamlMergeExpander.Expand(inputMerged);

            var merged = (YamlMappingNode)result.Root;
            merged.Children.Count.Should().Be(2);
            ((YamlScalarNode)merged.Children[new YamlScalarNode("x")]).Value.Should().Be("1");
            ((YamlScalarNode)merged.Children[new YamlScalarNode("y")]).Value.Should().Be("9");

            // Every resulting value has exactly one matching origin.
            result.Origins.Count.Should().Be(2);

            var xOrigin = result.GetOrigin(merged.Children[new YamlScalarNode("x")])!;
            xOrigin.Should().NotBeNull();
            xOrigin.DefinedAt.Should().Be(baseXValue.Start);
            xOrigin.AnchorChain.Should().Equal(new AnchorName("base"));
            xOrigin.UsedAt.Should().Be(mergeKeyNode.Start);
            xOrigin.IsLocal.Should().BeFalse();

            var yOrigin = result.GetOrigin(merged.Children[new YamlScalarNode("y")])!;
            yOrigin.Should().NotBeNull();
            yOrigin.DefinedAt.Should().Be(localYValue.Start);
            yOrigin.AnchorChain.Should().BeEmpty();
            yOrigin.IsLocal.Should().BeTrue();

            // The overridden y=2 from base left no origin behind.
            result.Origins.Values.Should().NotContain(o => o.DefinedAt == inputBase.Children[new YamlScalarNode("y")].Start);

            // The input tree is unchanged.
            inputMerged.Children.Count.Should().Be(2);
            inputMerged.Children.Keys.Cast<YamlScalarNode>().Select(k => k.Value).Should().Equal("<<", "y");
            inputBase.Children.Count.Should().Be(2);
        }

        [Fact]
        public void NestedMergeAccumulatesTheAnchorChain()
        {
            var yaml = Load(@"
a: &a
  k: a
  ka: 1
b: &b
  <<: *a
  k: b
root:
  <<: *b
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var inputA = (YamlMappingNode)root.Children[new YamlScalarNode("a")];
            var inputB = (YamlMappingNode)root.Children[new YamlScalarNode("b")];
            var inputRoot = (YamlMappingNode)root.Children[new YamlScalarNode("root")];
            var rootMergeKey = inputRoot.Children.Keys.Cast<YamlScalarNode>().Single(k => k.Value == "<<");

            var result = YamlMergeExpander.Expand(inputRoot);

            var expanded = (YamlMappingNode)result.Root;
            ((YamlScalarNode)expanded.Children[new YamlScalarNode("k")]).Value.Should().Be("b");
            ((YamlScalarNode)expanded.Children[new YamlScalarNode("ka")]).Value.Should().Be("1");
            result.Origins.Count.Should().Be(2);

            var kOrigin = result.GetOrigin(expanded.Children[new YamlScalarNode("k")])!;
            kOrigin.DefinedAt.Should().Be(inputB.Children[new YamlScalarNode("k")].Start);
            kOrigin.AnchorChain.Should().Equal(new AnchorName("b"));
            kOrigin.UsedAt.Should().Be(rootMergeKey.Start);

            var kaOrigin = result.GetOrigin(expanded.Children[new YamlScalarNode("ka")])!;
            kaOrigin.DefinedAt.Should().Be(inputA.Children[new YamlScalarNode("ka")].Start);
            kaOrigin.AnchorChain.Should().Equal(new AnchorName("b"), new AnchorName("a"));
        }

        [Fact]
        public void SharedAnchorReferencedFromMultiplePlacesIsNotACycle()
        {
            var yaml = Load(@"
shared: &shared
  v: 1
first:
  <<: *shared
second:
  <<: *shared
twice:
  <<: [*shared, *shared]
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            var result = YamlMergeExpander.Expand(root);

            var expanded = (YamlMappingNode)result.Root;
            var first = (YamlMappingNode)expanded.Children[new YamlScalarNode("first")];
            var second = (YamlMappingNode)expanded.Children[new YamlScalarNode("second")];
            var twice = (YamlMappingNode)expanded.Children[new YamlScalarNode("twice")];

            ((YamlScalarNode)first.Children[new YamlScalarNode("v")]).Value.Should().Be("1");
            ((YamlScalarNode)second.Children[new YamlScalarNode("v")]).Value.Should().Be("1");
            twice.Children.Count.Should().Be(1);

            var firstOrigin = result.GetOrigin(first.Children[new YamlScalarNode("v")])!;
            firstOrigin.AnchorChain.Should().Equal(new AnchorName("shared"));
            var secondOrigin = result.GetOrigin(second.Children[new YamlScalarNode("v")])!;
            secondOrigin.AnchorChain.Should().Equal(new AnchorName("shared"));

            // Each expansion branch owns independent containers and values.
            first.Children[new YamlScalarNode("v")].Should().NotBeSameAs(second.Children[new YamlScalarNode("v")]);
            ((YamlScalarNode)first.Children[new YamlScalarNode("v")]).Value = "changed";
            ((YamlScalarNode)second.Children[new YamlScalarNode("v")]).Value.Should().Be("1");
            var inputShared = (YamlMappingNode)root.Children[new YamlScalarNode("shared")];
            ((YamlScalarNode)inputShared.Children[new YamlScalarNode("v")]).Value.Should().Be("1");
        }

        [Fact]
        public void SelfReferencingAnchorIsReportedAsACycleWithPositions()
        {
            var yaml = Load(@"
root: &r
  <<: *r
  x: 1
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            Action act = () => YamlMergeExpander.Expand(root);

            var exception = act.Should().Throw<YamlException>().Which;
            exception.Message.Should().Contain("cycle");
            exception.Message.Should().Contain("'r'");
            exception.Message.Should().MatchRegex(@"line \d+, column \d+");
        }

        [Fact]
        public void MutuallyReferencingAnchorsAreReportedAsACycleShowingTheChain()
        {
            var yaml = Load(@"
a: &a
  <<: *b
b: &b
  <<: *a
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            Action act = () => YamlMergeExpander.Expand(root);

            var exception = act.Should().Throw<YamlException>().Which;
            exception.Message.Should().Contain("cycle");
            exception.Message.Should().Contain("'a'");
            exception.Message.Should().Contain("'b'");
            exception.Message.Should().MatchRegex(@"line \d+, column \d+");
        }

        [Fact]
        public void DuplicateExplicitKeysAreRejected()
        {
            var yaml = Load(@"
!!str y: 1
y: 2
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            root.Children.Count.Should().Be(2, "the keys have different tags, so parsing accepts them");

            Action act = () => YamlMergeExpander.Expand(root);

            act.Should().Throw<YamlException>().Which.Message.Should().Contain("Duplicate explicit key 'y'");
        }

        [Fact]
        public void ComplexKeysAreRejected()
        {
            var yaml = Load(@"
? [1, 2]
: v
plain: 1
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            Action act = () => YamlMergeExpander.Expand(root);

            act.Should().Throw<YamlException>().Which.Message.Should().Contain("scalar string keys");
        }

        [Fact]
        public void ExpansionBudgetIsEnforced()
        {
            var yaml = Load(@"
a: &a
  x: 1
  y: 2
m:
  <<: [*a, *a]
  z: 3
");
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var options = new MergeExpansionOptions { MaxExpandedNodes = 8 };

            Action act = () => YamlMergeExpander.Expand(root, options);

            act.Should().Throw<YamlException>().Which.Message.Should().Contain("anchor amplification");

            // The same document expands fine with the default budget.
            var result = YamlMergeExpander.Expand(root);
            var expanded = (YamlMappingNode)((YamlMappingNode)result.Root).Children[new YamlScalarNode("m")];
            expanded.Children.Count.Should().Be(3);
        }

        [Fact]
        public void ExpansionDoesNotModifyTheInputTree()
        {
            const string document = @"
base: &base
  x: 1
  y: 2
merged:
  <<: *base
  y: 9
";
            var yaml = Load(document);
            var before = Serialize(yaml);
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;

            var result = YamlMergeExpander.Expand(root);

            Serialize(yaml).Should().Be(before);

            // Mutating the result does not leak into the input tree.
            var expanded = (YamlMappingNode)result.Root;
            var expandedMerged = (YamlMappingNode)expanded.Children[new YamlScalarNode("merged")];
            expandedMerged.Children.Remove(new YamlScalarNode("x"));
            expandedMerged.Add("extra", "value");

            Serialize(yaml).Should().Be(before);
            var inputMerged = (YamlMappingNode)root.Children[new YamlScalarNode("merged")];
            inputMerged.Children.Count.Should().Be(2);
        }

        private static YamlStream Load(string document)
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(document));
            return yaml;
        }

        private static string Serialize(YamlStream yaml)
        {
            using var writer = new StringWriter();
            yaml.Save(writer, assignAnchors: false);
            return writer.ToString();
        }
    }
}
