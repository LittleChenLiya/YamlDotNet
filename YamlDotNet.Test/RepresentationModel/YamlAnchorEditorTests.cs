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

using System.IO;
using FluentAssertions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class YamlAnchorEditorTests
    {
        private const string RedefinedAnchorYaml =
            "first: &a 1\n" +
            "between: *a\n" +
            "second: &a 2\n" +
            "after: *a\n" +
            "# comment mentioning &a and *a\n" +
            "quoted: \"&a and *a stay\"\n" +
            "---\n" +
            "other: &a 9\n" +
            "ref: *a\n";

        [Fact]
        public void RenamingFirstOccurrenceOnlyTouchesItsOwnScope()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "a", "b", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "first: &b 1\n" +
                "between: *b\n" +
                "second: &a 2\n" +
                "after: *a\n" +
                "# comment mentioning &a and *a\n" +
                "quoted: \"&a and *a stay\"\n" +
                "---\n" +
                "other: &a 9\n" +
                "ref: *a\n");
        }

        [Fact]
        public void RenamingSecondOccurrenceOnlyTouchesItsOwnScope()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "a", "b", 2);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "first: &a 1\n" +
                "between: *a\n" +
                "second: &b 2\n" +
                "after: *b\n" +
                "# comment mentioning &a and *a\n" +
                "quoted: \"&a and *a stay\"\n" +
                "---\n" +
                "other: &a 9\n" +
                "ref: *a\n");
        }

        [Fact]
        public void RenameInSecondDocumentDoesNotTouchFirstDocument()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 1, "a", "doc2", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "first: &a 1\n" +
                "between: *a\n" +
                "second: &a 2\n" +
                "after: *a\n" +
                "# comment mentioning &a and *a\n" +
                "quoted: \"&a and *a stay\"\n" +
                "---\n" +
                "other: &doc2 9\n" +
                "ref: *doc2\n");
        }

        [Fact]
        public void RenameThatWouldCaptureAnotherAliasIsRejected()
        {
            var yaml =
                "y: &c 2\n" +
                "x: &a 1\n" +
                "z: *c\n" +
                "q: &c 3\n" +
                "w: *a\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "a", "c", 1);

            result.Status.Should().Be(AnchorRenameStatus.Conflict);
            result.NewText.Should().BeNull();
            result.Conflicts.Should().HaveCount(2);

            // "z: *c" would be captured by the renamed anchor.
            result.Conflicts[0].AliasName.Should().Be("c");
            result.Conflicts[0].Start.Line.Should().Be(3);
            result.Conflicts[0].Start.Column.Should().Be(4);

            // "w: *a" would be captured by the later "&c" definition on line 4.
            result.Conflicts[1].AliasName.Should().Be("a");
            result.Conflicts[1].Start.Line.Should().Be(5);
            result.Conflicts[1].Start.Column.Should().Be(4);
        }

        [Fact]
        public void RenameThatWouldBindAnUnboundAliasIsRejected()
        {
            // The alias precedes the definition, so renaming "&a" to "&c" cannot capture it.
            var ok = YamlAnchorEditor.RenameAnchor("z: *c\nx: &a 1\n", 0, "a", "c", 1);
            ok.Success.Should().BeTrue(ok.ErrorMessage);

            // Here the renamed anchor would bind the previously unbound alias.
            var rejected = YamlAnchorEditor.RenameAnchor("x: &a 1\nz: *c\n", 0, "a", "c", 1);
            rejected.Status.Should().Be(AnchorRenameStatus.Conflict);
            rejected.Conflicts.Should().ContainSingle()
                .Which.AliasName.Should().Be("c");
        }

        [Fact]
        public void SelfReferencingAliasIsRenamedAndCycleIsPreserved()
        {
            var yaml = "root: &a [*a, 1]\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "a", "b", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be("root: &b [*b, 1]\n");

            var stream = new YamlStream();
            stream.Load(new StringReader(result.NewText!));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var sequence = (YamlSequenceNode)root.Children[new YamlScalarNode("root")];
            ReferenceEquals(sequence.Children[0], sequence).Should().BeTrue();
        }

        [Fact]
        public void SharedNodeTopologyIsPreservedAfterRename()
        {
            var yaml =
                "x: &a {k: v}\n" +
                "y: *a\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "a", "shared", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "x: &shared {k: v}\n" +
                "y: *shared\n");

            AssertSharing(yaml);
            AssertSharing(result.NewText!);

            static void AssertSharing(string text)
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(text));
                var root = (YamlMappingNode)stream.Documents[0].RootNode;
                var x = root.Children[new YamlScalarNode("x")];
                var y = root.Children[new YamlScalarNode("y")];
                ReferenceEquals(x, y).Should().BeTrue();
            }
        }

        [Fact]
        public void CrlfLineEndingsArePreservedAndPositionsAreUtf16()
        {
            var yaml =
                "one: &a 1\r\n" +
                "two: *a\r\n" +
                "three: &a 2\r\n" +
                "four: *a\r\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "a", "bb", 2);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "one: &a 1\r\n" +
                "two: *a\r\n" +
                "three: &bb 2\r\n" +
                "four: *bb\r\n");
        }

        [Fact]
        public void UnicodeContentAndAnchorNamesAreHandled()
        {
            var yaml =
                "# 注释 with emoji \U0001F600 &no *no\n" +
                "key: &锚点 \"值 \U0001F600\"\n" +
                "ref: *锚点\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "锚点", "名前", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be(
                "# 注释 with emoji \U0001F600 &no *no\n" +
                "key: &名前 \"值 \U0001F600\"\n" +
                "ref: *名前\n");
        }

        [Fact]
        public void AnchorWithoutAliasesIsRenamed()
        {
            var yaml = "a: &lonely 1\nb: 2\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "lonely", "solo", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be("a: &solo 1\nb: 2\n");
        }

        [Fact]
        public void AnchorOnTaggedNodeIsRenamed()
        {
            var yaml = "a: !!str &a 1\nb: *a\n";

            var result = YamlAnchorEditor.RenameAnchor(yaml, 0, "a", "b", 1);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.NewText.Should().Be("a: !!str &b 1\nb: *b\n");
        }

        [Fact]
        public void MissingAnchorOccurrenceReturnsAnchorNotFound()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "a", "b", 3);

            result.Status.Should().Be(AnchorRenameStatus.AnchorNotFound);
            result.NewText.Should().BeNull();
        }

        [Fact]
        public void UnknownAnchorReturnsAnchorNotFound()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "nope", "b", 1);

            result.Status.Should().Be(AnchorRenameStatus.AnchorNotFound);
            result.NewText.Should().BeNull();
        }

        [Fact]
        public void MissingDocumentReturnsDocumentNotFound()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 7, "a", "b", 1);

            result.Status.Should().Be(AnchorRenameStatus.DocumentNotFound);
            result.NewText.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("a,b")]
        [InlineData("a]b")]
        [InlineData("a\tb")]
        public void InvalidNewNameIsRejected(string newName)
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "a", newName, 1);

            result.Status.Should().Be(AnchorRenameStatus.InvalidAnchorName);
            result.NewText.Should().BeNull();
        }

        [Fact]
        public void InvalidOldNameIsRejected()
        {
            var result = YamlAnchorEditor.RenameAnchor(RedefinedAnchorYaml, 0, "a{b", "b", 1);

            result.Status.Should().Be(AnchorRenameStatus.InvalidAnchorName);
            result.NewText.Should().BeNull();
        }

        [Fact]
        public void MalformedYamlReturnsSyntaxErrorWithoutPartialText()
        {
            var result = YamlAnchorEditor.RenameAnchor("a: [1, 2\nb: &a *a\n", 0, "a", "b", 1);

            result.Status.Should().Be(AnchorRenameStatus.SyntaxError);
            result.NewText.Should().BeNull();
        }
    }
}
