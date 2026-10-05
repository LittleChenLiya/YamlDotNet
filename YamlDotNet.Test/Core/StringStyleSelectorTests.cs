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
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization.Schemas;

namespace YamlDotNet.Test.Core
{
    public class StringStyleSelectorTests
    {
        [Theory]
        [InlineData("yes")]
        [InlineData("Yes")]
        [InlineData("YES")]
        [InlineData("no")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("on")]
        [InlineData("off")]
        [InlineData("null")]
        [InlineData("Null")]
        [InlineData("NULL")]
        [InlineData("~")]
        [InlineData("012")]
        [InlineData("123")]
        [InlineData("-123")]
        [InlineData("0x1F")]
        [InlineData("0o17")]
        [InlineData("1.5")]
        [InlineData(".inf")]
        [InlineData(".nan")]
        public void ReservedWordsAreQuoted(string value)
        {
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().BeOneOf(ScalarStyle.SingleQuoted, ScalarStyle.DoubleQuoted);
            selection.Reason.Should().Be(StringStyleReason.ReservedWord);
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("hello world")]
        [InlineData("has a \"quote\"")]
        [InlineData("a'b")]
        [InlineData("regular text")]
        public void PlainSafeValuesArePlain(string value)
        {
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().Be(ScalarStyle.Plain);
            selection.Reason.Should().Be(StringStyleReason.PlainSafe);
        }

        [Fact]
        public void EmptyStringIsSingleQuoted()
        {
            var selection = StringStyleSelector.Select(string.Empty, TagName.Empty);

            selection.Style.Should().Be(ScalarStyle.SingleQuoted);
            selection.Reason.Should().Be(StringStyleReason.Empty);
        }

        [Fact]
        public void EmptyStringWithNullTagIsPlain()
        {
            var selection = StringStyleSelector.Select(string.Empty, JsonSchema.Tags.Null);

            selection.Style.Should().Be(ScalarStyle.Plain);
            selection.Reason.Should().Be(StringStyleReason.PlainSafe);
        }

        [Theory]
        [InlineData("a: b")]
        [InlineData("a #b")]
        [InlineData(" leading")]
        [InlineData("trailing ")]
        [InlineData("[bracket]")]
        [InlineData("{brace}")]
        [InlineData("#hash")]
        [InlineData("- dash")]
        public void ValuesWithIndicatorsAreQuoted(string value)
        {
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().BeOneOf(ScalarStyle.SingleQuoted, ScalarStyle.DoubleQuoted);
            selection.Reason.Should().Be(StringStyleReason.Indicator);
        }

        [Theory]
        [InlineData("a\nb")]
        [InlineData("a\nb\n")]
        [InlineData("a\nb\n\n")]
        [InlineData("line1\nline2\nline3\n")]
        public void MultilineValuesAreLiteral(string value)
        {
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().Be(ScalarStyle.Literal);
            selection.Reason.Should().Be(StringStyleReason.Multiline);
        }

        [Fact]
        public void MultilineValueWithTrailingSpaceIsDoubleQuoted()
        {
            // A trailing space at the very end forbids block style.
            var value = "a\nb\n ";
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().Be(ScalarStyle.DoubleQuoted);
            selection.Reason.Should().Be(StringStyleReason.MultilineBlockNotAllowed);
        }

        [Theory]
        [InlineData("a\rb")]
        [InlineData("a\r\nb")]
        [InlineData("a\nb\rc")]
        public void MultilineValueWithCarriageReturnIsDoubleQuoted(string value)
        {
            // Block style normalizes '\r' to '\n' during parsing, so a value containing '\r'
            // cannot round-trip through literal style. Double-quoted style preserves '\r'.
            var selection = StringStyleSelector.Select(value, TagName.Empty);

            selection.Style.Should().Be(ScalarStyle.DoubleQuoted);
            selection.Reason.Should().Be(StringStyleReason.MultilineBlockNotAllowed);
        }

        [Fact]
        public void ExplicitStringTagAllowsPlainForReservedWords()
        {
            // With an explicit str tag, plain is safe even for reserved words because the
            // tag prevents implicit resolution.
            var selection = StringStyleSelector.Select("yes", FailsafeSchema.Tags.Str);

            selection.Style.Should().Be(ScalarStyle.Plain);
            selection.Reason.Should().Be(StringStyleReason.PlainSafe);
        }

        [Fact]
        public void ExplicitNonStringTagReturnsPlain()
        {
            var selection = StringStyleSelector.Select("123", JsonSchema.Tags.Int);

            selection.Style.Should().Be(ScalarStyle.Plain);
            selection.Reason.Should().Be(StringStyleReason.PlainSafe);
        }

        [Theory]
        [InlineData("yes", true)]
        [InlineData("012", true)]
        [InlineData("null", true)]
        [InlineData("hello", false)]
        [InlineData("a\nb", false)]
        public void IsReservedWordReturnsExpectedResult(string value, bool expected)
        {
            StringStyleSelector.IsReservedWord(value).Should().Be(expected);
        }

        [Theory]
        [InlineData("a\nb", true)]
        [InlineData("a\rb", true)]
        [InlineData("a\r\nb", true)]
        [InlineData("no line break", false)]
        public void HasLineBreakReturnsExpectedResult(string value, bool expected)
        {
            StringStyleSelector.HasLineBreak(value).Should().Be(expected);
        }

        [Fact]
        public void SelectionDeconstructsToStyleAndReason()
        {
            var selection = StringStyleSelector.Select("hello", TagName.Empty);

            var (style, reason) = selection;

            style.Should().Be(ScalarStyle.Plain);
            reason.Should().Be(StringStyleReason.PlainSafe);
        }

        [Fact]
        public void SelectThrowsWhenValueIsNull()
        {
            var action = () => StringStyleSelector.Select(null!, TagName.Empty);

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
