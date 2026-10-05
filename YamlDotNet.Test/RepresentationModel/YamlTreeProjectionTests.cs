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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class YamlTreeProjectionTests
    {
        private static YamlMappingNode CreateSharedSequenceGraph(out YamlSequenceNode sharedSequence)
        {
            sharedSequence = new YamlSequenceNode(new YamlScalarNode("item"));
            return new YamlMappingNode
            {
                { new YamlScalarNode("first"), sharedSequence },
                { new YamlScalarNode("second"), sharedSequence },
            };
        }

        [Fact]
        public void SharedSequenceExpandsToSevenNodesWhenBudgetAllows()
        {
            var root = CreateSharedSequenceGraph(out _);

            var status = YamlTreeProjection.TryProject(root, 7, out var result);

            status.Should().Be(YamlTreeProjectionStatus.Success);
            result.Should().NotBeNull();
            // 1 mapping + 2 keys + 2 sequences + 2 items = 7 nodes.
            result!.NodeCount.Should().Be(7);
            result.Root.AllNodes.Count().Should().Be(7);
            result.Provenance.Should().HaveCount(7);
        }

        [Fact]
        public void SharedSequenceExpansionProducesIndependentCopiesWithSharedProvenance()
        {
            var root = CreateSharedSequenceGraph(out var sharedSequence);

            var result = YamlTreeProjection.Project(root, 7);

            var projected = (YamlMappingNode)result.Root;
            var copies = projected.Children.Values.Cast<YamlSequenceNode>().ToList();
            copies.Should().HaveCount(2);
            copies[0].Should().NotBeSameAs(copies[1], "each occurrence must become an independent node");
            copies[0].Should().NotBeSameAs(sharedSequence);
            copies[1].Should().NotBeSameAs(sharedSequence);

            // Both copies trace back to the very same original node.
            result.GetOriginal(copies[0]).Should().BeSameAs(sharedSequence);
            result.GetOriginal(copies[1]).Should().BeSameAs(sharedSequence);
            result.Provenance[copies[0]].Should().BeSameAs(result.Provenance[copies[1]]);

            // The copies are structurally equal to the original.
            ((YamlNode)copies[0]).Should().Be(sharedSequence);
            ((YamlNode)copies[1]).Should().Be(sharedSequence);
        }

        [Fact]
        public void BudgetBelowExpansionCountIsRejectedAndInputStaysShared()
        {
            var root = CreateSharedSequenceGraph(out _);

            var status = YamlTreeProjection.TryProject(root, 6, out var result);

            status.Should().Be(YamlTreeProjectionStatus.BudgetExceeded);
            result.Should().BeNull("no result may be allocated when the budget is insufficient");

            // The source graph is untouched: the two values are still the same shared node.
            ReferenceEquals(root.Children[new YamlScalarNode("first")], root.Children[new YamlScalarNode("second")])
                .Should().BeTrue();
        }

        [Fact]
        public void ProjectThrowsWhenBudgetIsExceeded()
        {
            var root = CreateSharedSequenceGraph(out _);

            var project = new Action(() => YamlTreeProjection.Project(root, 6));

            project.Should().Throw<YamlTreeProjectionException>()
                .Which.Status.Should().Be(YamlTreeProjectionStatus.BudgetExceeded);
        }

        [Fact]
        public void SelfReferenceIsReportedAsCycle()
        {
            var sequence = new YamlSequenceNode();
            sequence.Add(sequence);

            var status = YamlTreeProjection.TryProject(sequence, 1000, out var result);

            status.Should().Be(YamlTreeProjectionStatus.CycleDetected);
            result.Should().BeNull();

            var project = new Action(() => YamlTreeProjection.Project(sequence, 1000));
            project.Should().Throw<YamlTreeProjectionException>()
                .Which.Status.Should().Be(YamlTreeProjectionStatus.CycleDetected);
        }

        [Fact]
        public void DeeplySharedGraphIsNotMistakenForACycle()
        {
            // A diamond: the leaf is shared by two sequences, which are shared by two mappings.
            var leaf = new YamlSequenceNode(new YamlScalarNode("leaf"));
            var middle = new YamlSequenceNode(leaf, leaf);
            var root = new YamlMappingNode
            {
                { new YamlScalarNode("a"), middle },
                { new YamlScalarNode("b"), middle },
            };

            // 1 mapping + 2 keys + 2 middles + 4 leaves + 4 leaf items = 13 nodes.
            var status = YamlTreeProjection.TryProject(root, 13, out var result);

            status.Should().Be(YamlTreeProjectionStatus.Success);
            result!.NodeCount.Should().Be(13);
            result.Root.AllNodes.Count().Should().Be(13);
        }

        [Fact]
        public void TagsAndStylesArePreserved()
        {
            var scalar = new YamlScalarNode("text")
            {
                Tag = new TagName("!greeting"),
                Style = ScalarStyle.SingleQuoted,
            };
            var sequence = new YamlSequenceNode(scalar)
            {
                Tag = new TagName("!items"),
                Style = SequenceStyle.Flow,
            };
            var root = new YamlMappingNode
            {
                Tag = new TagName("!root"),
                Style = MappingStyle.Flow,
            };
            root.Add(new YamlScalarNode("key"), sequence);

            var result = YamlTreeProjection.Project(root, 4);

            var projectedRoot = (YamlMappingNode)result.Root;
            projectedRoot.Tag.Should().Be(new TagName("!root"));
            projectedRoot.Style.Should().Be(MappingStyle.Flow);

            var projectedSequence = (YamlSequenceNode)projectedRoot.Children.Values.Single();
            projectedSequence.Tag.Should().Be(new TagName("!items"));
            projectedSequence.Style.Should().Be(SequenceStyle.Flow);

            var projectedScalar = (YamlScalarNode)projectedSequence.Children.Single();
            projectedScalar.Tag.Should().Be(new TagName("!greeting"));
            projectedScalar.Style.Should().Be(ScalarStyle.SingleQuoted);
            projectedScalar.Value.Should().Be("text");
        }

        [Fact]
        public void ProjectionDropsAnchorsAndAliasesButLeavesSourceAnchorsUntouched()
        {
            var root = CreateSharedSequenceGraph(out var sharedSequence);
            sharedSequence.Anchor = new AnchorName("shared");
            root.Anchor = new AnchorName("root");

            var result = YamlTreeProjection.Project(root, 7);

            // The projected tree contains no anchors and no aliases.
            foreach (var node in result.Root.AllNodes)
            {
                node.Anchor.IsEmpty.Should().BeTrue("the projected tree must not contain anchors");
                node.NodeType.Should().NotBe(YamlNodeType.Alias, "the projected tree must not contain aliases");
            }

            // The source graph keeps its anchors.
            root.Anchor.Should().Be(new AnchorName("root"));
            sharedSequence.Anchor.Should().Be(new AnchorName("shared"));
        }

        [Fact]
        public void ProjectionRoundTripsThroughTheEmitterWithoutAliases()
        {
            var root = CreateSharedSequenceGraph(out var sharedSequence);
            sharedSequence.Anchor = new AnchorName("shared");

            var result = YamlTreeProjection.Project(root, 7);

            var buffer = new System.Text.StringBuilder();
            using (var writer = new System.IO.StringWriter(buffer))
            {
                new YamlStream(new YamlDocument(result.Root)).Save(writer, assignAnchors: false);
            }

            var yaml = buffer.ToString();
            yaml.Should().NotContain("&shared", "anchors must not be emitted");
            yaml.Should().NotContain("*shared", "aliases must not be emitted");
        }

        [Fact]
        public void UnknownExtensionNodeIsRejected()
        {
            var root = new YamlSequenceNode(new ExtensionNode());

            var tryProject = new Action(() => YamlTreeProjection.TryProject(root, 10, out _));

            tryProject.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void UnresolvedAliasNodeIsRejected()
        {
            var root = new YamlSequenceNode(new YamlAliasNode(new AnchorName("dangling")));

            var tryProject = new Action(() => YamlTreeProjection.TryProject(root, 10, out _));

            tryProject.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void GetOriginalReturnsNullForNodesOutsideTheProjection()
        {
            var root = CreateSharedSequenceGraph(out _);

            var result = YamlTreeProjection.Project(root, 7);

            result.GetOriginal(root).Should().BeNull("the source root is not part of the projected tree");
            result.GetOriginal(new YamlScalarNode("stranger")).Should().BeNull();
            result.GetOriginal(result.Root).Should().BeSameAs(root);
        }

        [Fact]
        public void InvalidArgumentsAreRejected()
        {
            var root = CreateSharedSequenceGraph(out _);

            Action nullRoot = () => YamlTreeProjection.TryProject(null!, 10, out _);
            nullRoot.Should().Throw<ArgumentNullException>();

            Action negativeBudget = () => YamlTreeProjection.TryProject(root, -1, out _);
            negativeBudget.Should().Throw<ArgumentOutOfRangeException>();
        }

        private sealed class ExtensionNode : YamlNode
        {
            public override YamlNodeType NodeType => YamlNodeType.Alias;

            public override void Accept(IYamlVisitor visitor)
            {
                throw new NotSupportedException();
            }

            internal override void ResolveAliases(DocumentLoadingState state)
            {
                throw new NotSupportedException();
            }

            internal override void Emit(IEmitter emitter, YamlDotNet.RepresentationModel.EmitterState state)
            {
                throw new NotSupportedException();
            }

            internal override IEnumerable<YamlNode> SafeAllNodes(RecursionLevel level)
            {
                yield return this;
            }

            internal override string ToString(RecursionLevel level)
            {
                return "extension";
            }
        }
    }
}
