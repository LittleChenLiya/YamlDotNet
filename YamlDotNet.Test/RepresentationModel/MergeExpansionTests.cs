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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class MergeExpansionTests
    {
        private const string MergeSequenceYaml = """
            base: &base
              x: 1
              y: 2
            next: &next
              x: 3
            item:
              <<: [*base, *next]
              y: 9
            """;

        private readonly ITestOutputHelper output;

        public MergeExpansionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void MergeSequenceWithExplicitOverrideTracksValuesAndOrigins()
        {
            var document = LoadDocument(MergeSequenceYaml);

            var expansion = document.ExpandMerges();

            var item = (YamlMappingNode)((YamlMappingNode)expansion.Root).Children[new YamlScalarNode("item")];
            var x = item.Children[new YamlScalarNode("x")];
            var y = item.Children[new YamlScalarNode("y")];
            ((YamlScalarNode)x).Value.Should().Be("1", "because base appears before next in the merge sequence");
            ((YamlScalarNode)y).Value.Should().Be("9", "because explicit keys override merged keys");

            var xOrigin = expansion.OriginOf(x);
            xOrigin.IsLocal.Should().BeFalse();
            xOrigin.AnchorChain.Should().Equal(new[] { new AnchorName("base") });
            xOrigin.DefinitionStart.Line.Should().Be(2, "because x is ultimately defined in base");
            xOrigin.UseStart.Line.Should().Be(7, "because x is pulled in by the merge key of item");

            var yOrigin = expansion.OriginOf(y);
            yOrigin.IsLocal.Should().BeTrue();
            yOrigin.AnchorChain.Should().BeEmpty();
            yOrigin.DefinitionStart.Line.Should().Be(8, "because y is defined locally in item");
            yOrigin.UseStart.Line.Should().Be(8);
        }

        [Fact]
        public void OverriddenMergedValuesDoNotLeaveOriginsBehind()
        {
            var document = LoadDocument(MergeSequenceYaml);

            var expansion = document.ExpandMerges();

            var item = (YamlMappingNode)((YamlMappingNode)expansion.Root).Children[new YamlScalarNode("item")];
            var itemOrigins = item.AllNodes.Select(n => expansion.OriginOf(n)).ToList();
            itemOrigins.Should().NotContain(
                o => o.DefinitionStart.Line == 3,
                "because the y: 2 value from base is overridden and must not claim a result node");
            itemOrigins.Should().NotContain(
                o => o.DefinitionStart.Line == 5,
                "because the x: 3 value from next is overridden by the earlier base mapping");
            expansion.Origins.Count.Should().Be(expansion.Root.AllNodes.Count(),
                "because every result node has exactly one origin and vice versa");
        }

        [Fact]
        public void ExpansionDoesNotModifyTheInputTree()
        {
            var document = LoadDocument(MergeSequenceYaml);
            var inputRoot = (YamlMappingNode)document.RootNode;
            var inputNodeCount = inputRoot.AllNodes.Count();

            var expansion = document.ExpandMerges();

            inputRoot.AllNodes.Count().Should().Be(inputNodeCount);
            var inputItem = (YamlMappingNode)inputRoot.Children[new YamlScalarNode("item")];
            inputItem.Children.Should().HaveCount(2, "because the merge key must remain in the input tree");
            inputItem.Children.ContainsKey(new YamlScalarNode("<<")).Should().BeTrue();

            var resultItem = (YamlMappingNode)((YamlMappingNode)expansion.Root).Children[new YamlScalarNode("item")];
            resultItem.Children.ContainsKey(new YamlScalarNode("<<")).Should().BeFalse("because the merge key is expanded away in the result");
            resultItem.Children.Should().HaveCount(2);
            var inputNodes = new HashSet<YamlNode>(inputRoot.AllNodes, new YamlNodeIdentityEqualityComparer());
            expansion.Root.AllNodes.Should().NotContain(n => inputNodes.Contains(n),
                "because the result must be made of new, independent nodes");
        }

        [Fact]
        public void NestedMergesAccumulateTheAnchorChain()
        {
            var yaml = """
                a: &a
                  x: 1
                b: &b
                  <<: *a
                  y: 2
                c:
                  <<: *b
                  z: 3
                """;
            var document = LoadDocument(yaml);

            var expansion = document.ExpandMerges();

            var c = (YamlMappingNode)((YamlMappingNode)expansion.Root).Children[new YamlScalarNode("c")];
            var x = c.Children[new YamlScalarNode("x")];
            var y = c.Children[new YamlScalarNode("y")];
            var z = c.Children[new YamlScalarNode("z")];
            ((YamlScalarNode)x).Value.Should().Be("1");
            ((YamlScalarNode)y).Value.Should().Be("2");
            ((YamlScalarNode)z).Value.Should().Be("3");

            var xOrigin = expansion.OriginOf(x);
            xOrigin.AnchorChain.Should().Equal(new[] { new AnchorName("b"), new AnchorName("a") });
            xOrigin.DefinitionStart.Line.Should().Be(2);
            xOrigin.UseStart.Line.Should().Be(7, "because the use location is the outermost merge key");

            var yOrigin = expansion.OriginOf(y);
            yOrigin.AnchorChain.Should().Equal(new[] { new AnchorName("b") });
            yOrigin.DefinitionStart.Line.Should().Be(5);

            expansion.OriginOf(z).IsLocal.Should().BeTrue();
            expansion.OriginOf(z).DefinitionStart.Line.Should().Be(8);
        }

        [Fact]
        public void SharedAnchorsReferencedFromMultipleLocationsAreNotCycles()
        {
            var yaml = """
                def: &d
                  v: 1
                first:
                  <<: *d
                second:
                  <<: *d
                list:
                  - *d
                  - *d
                """;
            var document = LoadDocument(yaml);

            var expansion = document.ExpandMerges();

            var root = (YamlMappingNode)expansion.Root;
            var first = (YamlMappingNode)root.Children[new YamlScalarNode("first")];
            var second = (YamlMappingNode)root.Children[new YamlScalarNode("second")];
            var firstV = (YamlScalarNode)first.Children[new YamlScalarNode("v")];
            var secondV = (YamlScalarNode)second.Children[new YamlScalarNode("v")];
            firstV.Value.Should().Be("1");
            secondV.Value.Should().Be("1");
            expansion.OriginOf(firstV).AnchorChain.Should().Equal(new[] { new AnchorName("d") });
            expansion.OriginOf(firstV).DefinitionStart.Line.Should().Be(2);
            expansion.OriginOf(secondV).AnchorChain.Should().Equal(new[] { new AnchorName("d") });

            var list = (YamlSequenceNode)root.Children[new YamlScalarNode("list")];
            list.Children.Should().HaveCount(2);
            ReferenceEquals(list.Children[0], list.Children[1]).Should().BeFalse(
                "because each expansion branch owns independent containers");

            // Mutating one branch of the result affects neither the other branch nor the input.
            firstV.Value = "changed";
            ((YamlMappingNode)list.Children[0]).Children.Add(new YamlScalarNode("extra"), new YamlScalarNode("x"));
            secondV.Value.Should().Be("1");
            ((YamlMappingNode)list.Children[1]).Children.Should().HaveCount(1);
            var inputDef = (YamlMappingNode)((YamlMappingNode)document.RootNode).Children[new YamlScalarNode("def")];
            ((YamlScalarNode)inputDef.Children[new YamlScalarNode("v")]).Value.Should().Be("1");
            inputDef.Children.Should().HaveCount(1);
        }

        [Fact]
        public void SelfReferencingMergeReportsTheAnchorChainAndPosition()
        {
            var yaml = """
                root: &r
                  <<: *r
                """;
            var document = LoadDocument(yaml);

            var expand = () => document.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*'r' -> 'r'*line 2*column 3*");
        }

        [Fact]
        public void IndirectMergeCyclesReportTheFullAnchorChain()
        {
            var yaml = """
                a: &a
                  <<: *b
                b: &b
                  <<: *a
                """;
            var document = LoadDocument(yaml);

            var expand = () => document.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*'a' -> 'b' -> 'a'*");
        }

        [Fact]
        public void SelfReferencingSequencesAreReportedAsCycles()
        {
            var document = LoadDocument("root: &s [1, *s]");

            var expand = () => document.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*'s' -> 's'*");
        }

        [Fact]
        public void DuplicateExplicitKeysAreRejected()
        {
            var mapping = new YamlMappingNode(
                new KeyValuePair<YamlNode, YamlNode>(new YamlScalarNode("key"), new YamlScalarNode("1")),
                new KeyValuePair<YamlNode, YamlNode>(
                    new YamlScalarNode("key") { Tag = new TagName("tag:yaml.org,2002:str") },
                    new YamlScalarNode("2")));

            var expand = () => mapping.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*Duplicate explicit key 'key'*");
        }

        [Fact]
        public void ComplexKeysAreRejectedInsteadOfBeingStringified()
        {
            var yaml = """
                ? [1, 2]
                : value
                """;
            var document = LoadDocument(yaml);

            var expand = () => document.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*scalar string keys*");
        }

        [Fact]
        public void InvalidMergeValuesAreRejected()
        {
            var document = LoadDocument("""
                root:
                  <<: 42
                """);

            var expand = () => document.ExpandMerges();

            expand.Should().Throw<YamlException>()
                .WithMessage("*Unrecognized merge key pattern*");
        }

        [Fact]
        public void ExpansionBudgetProtectsAgainstAnchorAmplification()
        {
            const string yaml = """
                a: &a [1, 2, 3]
                b: &b [*a, *a, *a]
                c: &c [*b, *b, *b]
                d: [*c, *c, *c]
                """;
            var document = LoadDocument(yaml);
            var inputNodeCount = document.RootNode.AllNodes.Count();

            var expand = () => document.ExpandMerges(new YamlMergeExpansionOptions { MaxExpandedNodes = 50 });

            expand.Should().Throw<YamlException>()
                .WithMessage("*limit of 50*");

            var expansion = document.ExpandMerges();
            expansion.Root.AllNodes.Count().Should().Be(183);
            expansion.Origins.Count.Should().Be(183);
            document.RootNode.AllNodes.Count().Should().Be(inputNodeCount,
                "because the input tree keeps its original, unexpanded shape");
        }

        [Fact]
        public void DemonstratesPathsValuesAndOrigins()
        {
            var document = LoadDocument(MergeSequenceYaml);

            var expansion = document.ExpandMerges();

            var lines = new List<string>();
            CollectLines(expansion, expansion.Root, "root", lines);
            foreach (var line in lines)
            {
                output.WriteLine(line);
            }

            lines.Should().Contain(l => l.StartsWith("root.item.x = 1", StringComparison.Ordinal) && l.Contains("line 2"));
            lines.Should().Contain(l => l.StartsWith("root.item.y = 9", StringComparison.Ordinal) && l.Contains("line 8"));
        }

        private static void CollectLines(YamlMergeExpansion expansion, YamlNode node, string path, List<string> lines)
        {
            var origin = expansion.OriginOf(node);
            switch (node)
            {
                case YamlScalarNode scalar:
                    lines.Add($"{path} = {scalar.Value}  |  {origin}");
                    break;
                case YamlSequenceNode sequence:
                    lines.Add($"{path}  |  {origin}");
                    for (var i = 0; i < sequence.Children.Count; i++)
                    {
                        CollectLines(expansion, sequence.Children[i], $"{path}[{i}]", lines);
                    }

                    break;
                case YamlMappingNode mapping:
                    lines.Add($"{path}  |  {origin}");
                    foreach (var child in mapping.Children)
                    {
                        var keyName = child.Key is YamlScalarNode keyScalar ? keyScalar.Value : "?";
                        CollectLines(expansion, child.Value, $"{path}.{keyName}", lines);
                    }

                    break;
            }
        }

        private static YamlDocument LoadDocument(string yaml)
        {
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText(yaml));
            return stream.Documents[0];
        }
    }
}
