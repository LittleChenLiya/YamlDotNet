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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace YamlDotNet.Test.Core
{
    /// <summary>
    /// Integration tests for the <see cref="StringStyleSelector"/> when used through the
    /// <see cref="Emitter"/>. These tests verify that the emitted scalar round-trips to the
    /// original string when re-parsed.
    /// </summary>
    public class EmitterStringStyleSelectorTests : EmitterTestsHelper
    {
        [Theory]
        [InlineData("yes")]
        [InlineData("012")]
        [InlineData("null")]
        [InlineData("hello")]
        [InlineData("a\nb\n\n")]
        [InlineData("  indented\nline2\n")]
        [InlineData("trailing ")]
        [InlineData("has a \"quote\"")]
        public void ScalarRoundTripsWithSelector(string value)
        {
            var yaml = EmitWithSelector(value, "\n");

            var deserialized = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize(yaml);

            deserialized.Should().Be(value);
        }

        [Fact]
        public void ReservedWordIsQuoted()
        {
            var yaml = EmitWithSelector("yes", "\n");

            yaml.Should().Contain("'yes'");
        }

        [Fact]
        public void NumericStringIsQuoted()
        {
            var yaml = EmitWithSelector("012", "\n");

            yaml.Should().Contain("'012'");
        }

        [Fact]
        public void MultilineValueUsesLiteralStyle()
        {
            var yaml = EmitWithSelector("a\nb\n", "\n");

            yaml.Should().Contain("|");
        }

        [Fact]
        public void MultilineValueWithTwoTrailingBreaksUsesKeepChomping()
        {
            var yaml = EmitWithSelector("a\nb\n\n", "\n");

            yaml.Should().Contain("|+");
        }

        [Fact]
        public void MultilineValueWithFirstLineIndentUsesIndentHint()
        {
            var yaml = EmitWithSelector("  indented\nline2\n", "\n");

            yaml.Should().Contain("|2");
        }

        [Fact]
        public void CarriageReturnRoundTripsWithSelector()
        {
            // Block style normalizes '\r' to '\n', so the selector must use double-quoted
            // style to preserve the carriage return and guarantee round-trip.
            var yaml = EmitWithSelector("a\rb\n", "\n");

            yaml.Should().Contain("\"");

            var deserialized = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize(yaml);
            deserialized.Should().Be("a\rb\n");
        }

        [Fact]
        public void CrlfOutputPreservesInternalLf()
        {
            var yaml = EmitWithSelector("a\nb\n", "\r\n");

            // The structural break after the indicator uses CRLF, but the content breaks
            // inside the literal block must be bare LF.
            var lines = yaml.Split('\n');
            for (var i = 1; i < lines.Length - 1; i++)
            {
                if (lines[i].Length > 0 && lines[i][0] == ' ')
                {
                    lines[i].Should().NotEndWith("\r", "content breaks must be bare LF");
                }
            }

            // Round-trip still works.
            var deserialized = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize(yaml);
            deserialized.Should().Be("a\nb\n");
        }

        [Fact]
        public void CrlfOutputWithTwoTrailingBreaksPreservesInternalLf()
        {
            var yaml = EmitWithSelector("a\nb\n\n", "\r\n");

            yaml.Should().Contain("|+");

            var deserialized = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize(yaml);
            deserialized.Should().Be("a\nb\n\n");
        }

        [Fact]
        public void DefaultEmitterDoesNotUseSelector()
        {
            // Without the selector, "yes" is emitted plainly (legacy behavior).
            var yaml = EmittedTextFrom(StreamOf(DocumentWith(new Scalar("yes"))));

            yaml.Trim().Should().Be("yes");
        }

        private string EmitWithSelector(string value, string newLine)
        {
            var settings = new EmitterSettings(
                bestIndent: 2,
                bestWidth: int.MaxValue,
                isCanonical: false,
                maxSimpleKeyLength: 1024,
                skipAnchorName: false,
                indentSequences: false,
                newLine: newLine,
                useUtf16SurrogatePairs: false,
                useStringStyleSelector: true);

            var writer = new StringWriter { NewLine = newLine };
            var emitter = new Emitter(writer, settings);
            StreamOf(DocumentWith(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Any, true, true))).Run(emitter.Emit);
            return writer.ToString();
        }
    }
}
