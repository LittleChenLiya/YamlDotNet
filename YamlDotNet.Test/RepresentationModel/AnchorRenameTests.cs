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
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class AnchorRenameTests
    {
        [Fact]
        public void RenameFirstOfTwoDefinitionsOnlyTouchesItsOwnScope()
        {
            var yaml =
                "first: &a 1\n" +
                "between: *a\n" +
                "second: &a 2\n" +
                "after: *a\n" +
                "---\n" +
                "other: &a 3\n" +
                "ref: *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            result.Should().Be(
                "first: &b 1\n" +
                "between: *b\n" +
                "second: &a 2\n" +
                "after: *a\n" +
                "---\n" +
                "other: &a 3\n" +
                "ref: *a\n");
        }

        [Fact]
        public void RenameSecondOccurrenceOnlyTouchesItsOwnScope()
        {
            var yaml =
                "first: &a 1\n" +
                "between: *a\n" +
                "second: &a 2\n" +
                "after: *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b", occurrence: 2);

            result.Should().Be(
                "first: &a 1\n" +
                "between: *a\n" +
                "second: &b 2\n" +
                "after: *b\n");
        }

        [Fact]
        public void CommentsAndQuotedStringsAreNotTouched()
        {
            var yaml =
                "value: &a 1 # a comment with &a and *a\n" +
                "ref: *a\n" +
                "quoted: \"&a and *a stay\"\n" +
                "single: '&a *a'\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "renamed");

            result.Should().Be(
                "value: &renamed 1 # a comment with &a and *a\n" +
                "ref: *renamed\n" +
                "quoted: \"&a and *a stay\"\n" +
                "single: '&a *a'\n");
        }

        [Fact]
        public void RenameThatWouldCaptureAnotherAliasIsRejected()
        {
            var yaml =
                "first: &b 0\n" +
                "target: &a 1\n" +
                "kept: *b\n" +
                "ref: *a\n";

            // Renaming &a to &b would make "kept: *b" resolve to the renamed node.
            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            var exception = act.Should().Throw<AnchorRenameConflictException>().Which;
            exception.ConflictPositions.Should().ContainSingle();
            exception.ConflictPositions[0].Line.Should().Be(3);
            exception.ConflictPositions[0].Column.Should().Be(7);
        }

        [Fact]
        public void RenameThatWouldLetAliasBeCapturedByLaterDefinitionIsRejected()
        {
            var yaml =
                "target: &a 1\n" +
                "other: &b 2\n" +
                "ref: *a\n";

            // After renaming &a to &b, "*a" -> "*b" would bind to "&b 2" instead of the renamed node.
            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            var exception = act.Should().Throw<AnchorRenameConflictException>().Which;
            exception.ConflictPositions.Should().ContainSingle();
            exception.ConflictPositions[0].Line.Should().Be(3);
        }

        [Fact]
        public void RenamingToSameNameIsANoOp()
        {
            var yaml = "value: &a 1\nref: *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "a");

            result.Should().Be(yaml);
        }

        [Fact]
        public void SelfReferenceIsPreserved()
        {
            var yaml = "root: &a\n  - *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "loop");

            result.Should().Be("root: &loop\n  - *loop\n");

            var stream = new YamlStream();
            stream.Load(new StringReader(result));
            var sequence = (YamlSequenceNode)((YamlMappingNode)stream.Documents[0].RootNode)["root"];
            sequence.Children[0].Should().BeSameAs(sequence);
        }

        [Fact]
        public void SharedGraphTopologyIsPreserved()
        {
            var yaml =
                "x: &a\n  inner: 1\n" +
                "y: *a\n" +
                "z: *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "shared");

            result.Should().Be(
                "x: &shared\n  inner: 1\n" +
                "y: *shared\n" +
                "z: *shared\n");

            var stream = new YamlStream();
            stream.Load(new StringReader(result));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var x = root["x"];
            root["y"].Should().BeSameAs(x);
            root["z"].Should().BeSameAs(x);
        }

        [Fact]
        public void CrlfLineEndingsArePreserved()
        {
            var yaml = "a: &x 1\r\nb: *x\r\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "x", "y");

            result.Should().Be("a: &y 1\r\nb: *y\r\n");
        }

        [Fact]
        public void UnicodeContentAndAnchorNamesAreHandled()
        {
            var yaml = "\u503c: &\u951A 1 \U0001F600\n\u5F15\u7528: *\u951A\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "\u951A", "\u65B0\u951A");

            result.Should().Be("\u503c: &\u65B0\u951A 1 \U0001F600\n\u5F15\u7528: *\u65B0\u951A\n");
        }

        [Fact]
        public void AnchorWithoutAliasIsRenamed()
        {
            var yaml = "lonely: &a 1\nother: 2\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            result.Should().Be("lonely: &b 1\nother: 2\n");
        }

        [Fact]
        public void AnchorAfterTagIsRenamed()
        {
            var yaml = "value: !!str &a 1\nref: *a\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            result.Should().Be("value: !!str &b 1\nref: *b\n");
        }

        [Fact]
        public void MissingAnchorThrows()
        {
            var yaml = "value: &a 1\n";

            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "missing", "b");

            act.Should().Throw<AnchorNotFoundException>();
        }

        [Fact]
        public void MissingOccurrenceThrows()
        {
            var yaml = "value: &a 1\n";

            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b", occurrence: 2);

            act.Should().Throw<AnchorNotFoundException>();
        }

        [Fact]
        public void MissingDocumentThrows()
        {
            var yaml = "value: &a 1\n";

            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 1, "a", "b");

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("a,b")]
        [InlineData("a]b")]
        public void InvalidNewNameThrows(string newName)
        {
            var yaml = "value: &a 1\n";

            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "a", newName);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SyntaxErrorThrowsWithoutPartialResult()
        {
            var yaml = "value: [1, 2\n  : broken";

            var act = () => YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "b");

            act.Should().Throw<YamlException>();
        }

        [Fact]
        public void OnlyTokenBytesChange()
        {
            var yaml =
                "# header &a *a\n" +
                "one: &aa 1\n" +
                "two: *aa\n" +
                "three: *aa\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "aa", "bb");

            // Same length, and the only differing spans are the three renamed tokens.
            result.Should().HaveLength(yaml.Length);
            result.Should().Be(
                "# header &a *a\n" +
                "one: &bb 1\n" +
                "two: *bb\n" +
                "three: *bb\n");
        }

        [Fact]
        public void SimilarAnchorNamesAreNotConfused()
        {
            var yaml =
                "one: &a 1\n" +
                "two: &ab 2\n" +
                "r1: *a\n" +
                "r2: *ab\n";

            var result = YamlDocumentEditor.RenameAnchor(yaml, 0, "a", "c");

            result.Should().Be(
                "one: &c 1\n" +
                "two: &ab 2\n" +
                "r1: *c\n" +
                "r2: *ab\n");
        }
    }
}
