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
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class YamlNodeTreeProjectionTests
    {
        /// <summary>
        /// Counts the total number of nodes in a tree (acyclic) by traversing it.
        /// Uses reference equality to detect shared references (not value equality, which YamlScalarNode overrides).
        /// </summary>
        private static int CountTreeNodes(YamlNode root)
        {
            var count = 0;
            var visited = new List<YamlNode>();
            var stack = new Stack<YamlNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (visited.Any(n => ReferenceEquals(n, node)))
                {
                    throw new InvalidOperationException("The projected tree contains a shared reference or cycle, which should not happen.");
                }
                visited.Add(node);
                count++;
                switch (node)
                {
                    case YamlSequenceNode sequence:
                        foreach (var child in sequence.Children)
                        {
                            stack.Push(child);
                        }
                        break;
                    case YamlMappingNode mapping:
                        foreach (var entry in mapping.Children)
                        {
                            stack.Push(entry.Key);
                            stack.Push(entry.Value);
                        }
                        break;
                }
            }
            return count;
        }

        /// <summary>
        /// Asserts that the specified tree contains no anchors and no alias nodes.
        /// Uses reference equality to detect shared references (not value equality, which YamlScalarNode overrides).
        /// </summary>
        private static void AssertIsAliasFreeTree(YamlNode root)
        {
            var visited = new List<YamlNode>();
            var stack = new Stack<YamlNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (visited.Any(n => ReferenceEquals(n, node)))
                {
                    throw new InvalidOperationException("The projected tree contains a shared reference, which should not happen.");
                }
                visited.Add(node);
                node.Anchor.IsEmpty.Should().BeTrue("because the projected tree must not contain anchors.");
                node.NodeType.Should().NotBe(YamlNodeType.Alias, "because the projected tree must not contain aliases.");
                switch (node)
                {
                    case YamlSequenceNode sequence:
                        foreach (var child in sequence.Children)
                        {
                            stack.Push(child);
                        }
                        break;
                    case YamlMappingNode mapping:
                        foreach (var entry in mapping.Children)
                        {
                            stack.Push(entry.Key);
                            stack.Push(entry.Value);
                        }
                        break;
                }
            }
        }

        [Fact]
        public void TwoKeysSharingASingleElementSequence_ExpandsToSevenNodes()
        {
            // Arrange: two keys share a single-element sequence node.
            // Graph: root mapping -> { a: S, b: S } where S -> [ x ].
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            // Sanity check: the input graph is shared (both values reference the same sequence node).
            var sharedSequence = (YamlSequenceNode)root.Children[0].Value;
            ReferenceEquals(sharedSequence, root.Children[1].Value).Should().BeTrue("because the input graph is shared.");

            // Act & Assert: the expansion count is 7.
            YamlNodeTreeProjection.CountExpansion(root).Should().Be(7);
        }

        [Fact]
        public void TwoKeysSharingASingleElementSequence_BudgetSixRejects()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            // Act
            var act = new Action(() => YamlNodeTreeProjection.Project(root, 6));

            // Assert
            act.Should().Throw<InvalidOperationException>("because the projected tree would contain 7 nodes, exceeding the budget of 6.");
        }

        [Fact]
        public void TwoKeysSharingASingleElementSequence_BudgetSevenSucceeds()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var sharedSequence = (YamlSequenceNode)root.Children[0].Value;

            // Act
            var projected = YamlNodeTreeProjection.Project(root, 7);

            // Assert: the projected tree has 7 nodes.
            CountTreeNodes(projected).Should().Be(7);

            // Assert: the input graph is still shared (source graph and anchors unchanged).
            ReferenceEquals(sharedSequence, root.Children[1].Value).Should().BeTrue("because the source graph must remain unchanged.");
            sharedSequence.Anchor.IsEmpty.Should().BeFalse("because the source anchor must remain unchanged.");

            // Assert: the projected tree is alias-free.
            AssertIsAliasFreeTree(projected);
        }

        [Fact]
        public void TwoKeysSharingASingleElementSequence_CopiesAreIndependentAndRecordOriginalIdentity()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var sharedSequence = (YamlSequenceNode)root.Children[0].Value;
            var sharedScalar = (YamlScalarNode)sharedSequence.Children[0];

            // Act
            var projected = (YamlMappingNode)YamlNodeTreeProjection.Project(root, 7);

            // Assert: the two values are independent copies (not the same instance).
            var copyA = (YamlSequenceNode)projected.Children[0].Value;
            var copyB = (YamlSequenceNode)projected.Children[1].Value;
            ReferenceEquals(copyA, copyB).Should().BeFalse("because shared nodes must be copied into independent nodes.");

            // Assert: each copy records the original node it was created from.
            copyA.OriginalNode.Should().BeSameAs(sharedSequence, "because the copy must point back to the original node.");
            copyB.OriginalNode.Should().BeSameAs(sharedSequence, "because the copy must point back to the original node.");

            // Assert: the children of the copies are also independent and record their original identity.
            var copyScalarA = (YamlScalarNode)copyA.Children[0];
            var copyScalarB = (YamlScalarNode)copyB.Children[0];
            ReferenceEquals(copyScalarA, copyScalarB).Should().BeFalse("because shared nodes must be copied into independent nodes.");
            copyScalarA.OriginalNode.Should().BeSameAs(sharedScalar, "because the copy must point back to the original node.");
            copyScalarB.OriginalNode.Should().BeSameAs(sharedScalar, "because the copy must point back to the original node.");

            // Assert: the copies are separate from the source.
            ReferenceEquals(copyA, sharedSequence).Should().BeFalse();
            ReferenceEquals(copyB, sharedSequence).Should().BeFalse();
        }

        [Fact]
        public void SelfReference_ThrowsCycleException()
        {
            // Arrange: a mapping that contains itself as a value (a real cycle).
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("&x\na: 1\nb: *x"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            // Sanity check: the graph is self-referential.
            ReferenceEquals(root, root.Children[1].Value).Should().BeTrue("because the graph is self-referential.");

            // Act
            var act = new Action(() => YamlNodeTreeProjection.Project(root, 100));

            // Assert
            act.Should().Throw<InvalidOperationException>("because a self-referential graph contains a real cycle.");
        }

        [Fact]
        public void SharedReference_IsNotMistakenForCycle()
        {
            // Arrange: a DAG where a node is shared but there is no cycle.
            // root -> { a: S, b: S } where S -> [ x ].
            var seq = new YamlSequenceNode(new YamlScalarNode("x"));
            var root = new YamlMappingNode
            {
                { "a", seq },
                { "b", seq }
            };

            // Act
            var count = YamlNodeTreeProjection.CountExpansion(root);

            // Assert: the shared reference is counted once per occurrence, not mistaken for a cycle.
            count.Should().Be(7);
        }

        [Fact]
        public void TagAndScalarStyle_ArePreserved()
        {
            // Arrange
            var scalar = new YamlScalarNode("value")
            {
                Tag = new TagName("!custom"),
                Style = ScalarStyle.SingleQuoted
            };
            var root = new YamlSequenceNode(scalar);

            // Act
            var projected = (YamlSequenceNode)YamlNodeTreeProjection.Project(root, 10);
            var copy = (YamlScalarNode)projected.Children[0];

            // Assert
            copy.Tag.Should().Be(new TagName("!custom"), "because the tag must be preserved.");
            copy.Style.Should().Be(ScalarStyle.SingleQuoted, "because the scalar style must be preserved.");
            copy.Value.Should().Be("value");
            copy.OriginalNode.Should().BeSameAs(scalar);
        }

        [Fact]
        public void UnknownExtensionNode_IsRejected()
        {
            // Arrange: an alias node is an unknown extension node for the projection.
            var alias = new YamlAliasNode(new AnchorName("x"));
            var root = new YamlSequenceNode(alias);

            // Act
            var act = new Action(() => YamlNodeTreeProjection.Project(root, 10));

            // Assert
            act.Should().Throw<NotSupportedException>("because alias nodes are unknown extension nodes.");
        }

        [Fact]
        public void TryProject_ReturnsFalseWhenBudgetExceeded()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            // Act
            var result = YamlNodeTreeProjection.TryProject(root, 6, out var projected);

            // Assert
            result.Should().BeFalse("because the budget is exceeded.");
            projected.Should().BeNull("because no result should be allocated when the budget is exceeded.");
        }

        [Fact]
        public void TryProject_ReturnsTrueWhenBudgetSufficient()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            // Act
            var result = YamlNodeTreeProjection.TryProject(root, 7, out var projected);

            // Assert
            result.Should().BeTrue("because the budget is sufficient.");
            projected.Should().NotBeNull();
            CountTreeNodes(projected!).Should().Be(7);
        }

        [Fact]
        public void NullRoot_ThrowsArgumentNullException()
        {
            // Act
            var act = new Action(() => YamlNodeTreeProjection.Project(null!, 10));

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void NegativeBudget_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var root = new YamlScalarNode("x");

            // Act
            var act = new Action(() => YamlNodeTreeProjection.Project(root, -1));

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ScalarRoot_ProjectsToSingleNode()
        {
            // Arrange
            var root = new YamlScalarNode("hello");

            // Act
            var projected = YamlNodeTreeProjection.Project(root, 10);

            // Assert
            CountTreeNodes(projected).Should().Be(1);
            projected.Should().BeOfType<YamlScalarNode>();
            ((YamlScalarNode)projected).Value.Should().Be("hello");
            projected.OriginalNode.Should().BeSameAs(root);
        }

        [Fact]
        public void ProjectedTree_CanBeEmittedAsYamlWithoutAnchorsOrAliases()
        {
            // Arrange
            var stream = new YamlStream();
            stream.Load(Yaml.ParserForText("a: &s [x]\nb: *s"));
            var root = stream.Documents[0].RootNode;

            // Act
            var projected = YamlNodeTreeProjection.Project(root, 7);
            var yaml = new YamlStream(new YamlDocument(projected));
            var writer = new System.IO.StringWriter();
            yaml.Save(writer, assignAnchors: false);
            var output = writer.ToString();

            // Assert: the output contains no anchors or aliases.
            output.Should().NotContain("&", "because the projected tree must not contain anchors.");
            output.Should().NotContain("*", "because the projected tree must not contain aliases.");
            output.Should().Contain("x", "because the scalar value must be preserved.");
        }
    }
}
