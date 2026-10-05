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
    public class StringStyleSelectorTests
    {
        [Theory]
        [InlineData("yes")]
        [InlineData("no")]
        [InlineData("on")]
        [InlineData("off")]
        [InlineData("y")]
        [InlineData("n")]
        [InlineData("true")]
        [InlineData("False")]
        public void BooleanLikeStringsAreQuoted(string value)
        {
            var decision = StringStyleSelector.Select(value);

            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().Contain("boolean");
        }

        [Theory]
        [InlineData("null")]
        [InlineData("Null")]
        [InlineData("NULL")]
        [InlineData("~")]
        public void NullLikeStringsAreQuoted(string value)
        {
            var decision = StringStyleSelector.Select(value);

            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().Contain("null");
        }

        [Theory]
        [InlineData("012")] // leading zero: octal in YAML 1.1
        [InlineData("42")]
        [InlineData("-7")]
        [InlineData("0x1F")]
        [InlineData("0o17")]
        [InlineData("0b101")]
        [InlineData("1:20")] // sexagesimal
        public void NumberLikeStringsAreQuoted(string value)
        {
            var decision = StringStyleSelector.Select(value);

            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().Contain("integer");
        }

        [Theory]
        [InlineData("1.5")]
        [InlineData(".5")]
        [InlineData("1e10")]
        [InlineData(".inf")]
        [InlineData("-.Inf")]
        [InlineData(".nan")]
        public void FloatLikeStringsAreQuoted(string value)
        {
            var decision = StringStyleSelector.Select(value);

            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().Contain("floating point");
        }

        [Fact]
        public void EmptyStringIsQuoted()
        {
            var decision = StringStyleSelector.Select(string.Empty);

            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().Contain("null");
        }

        [Fact]
        public void OrdinaryStringsArePlain()
        {
            var decision = StringStyleSelector.Select("just some words");

            decision.Style.Should().Be(ScalarStyle.Plain);
            decision.Reason.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void SchemaDeterminesWhichWordsAreImplicitlyTyped()
        {
            // "yes" is only a boolean in the YAML 1.1 schema.
            StringStyleSelector.Select("yes", StringScalarSchema.Yaml11).Style.Should().Be(ScalarStyle.SingleQuoted);
            StringStyleSelector.Select("yes", StringScalarSchema.Core).Style.Should().Be(ScalarStyle.Plain);
            StringStyleSelector.Select("yes", StringScalarSchema.Failsafe).Style.Should().Be(ScalarStyle.Plain);

            // "012" is a number in both the YAML 1.1 and the core schemas.
            StringStyleSelector.Select("012", StringScalarSchema.Yaml11).Style.Should().Be(ScalarStyle.SingleQuoted);
            StringStyleSelector.Select("012", StringScalarSchema.Core).Style.Should().Be(ScalarStyle.SingleQuoted);
        }

        [Fact]
        public void TimestampsAreQuotedInYaml11()
        {
            StringStyleSelector.Select("2001-12-15", StringScalarSchema.Yaml11).Style.Should().Be(ScalarStyle.SingleQuoted);
            StringStyleSelector.Select("2001-12-15", StringScalarSchema.Core).Style.Should().Be(ScalarStyle.Plain);
        }

        [Fact]
        public void MultilineStringsAreLiteral()
        {
            var decision = StringStyleSelector.Select("line one\nline two");

            decision.Style.Should().Be(ScalarStyle.Literal);
            decision.Reason.Should().Contain("literal");
        }

        [Fact]
        public void TrailingLineBreaksSelectTheChompingIndicator()
        {
            StringStyleSelector.Select("text\n\n").Reason.Should().Contain("keep chomping");
            StringStyleSelector.Select("text\n").Reason.Should().Contain("clip chomping");
            StringStyleSelector.Select("text\nmore").Reason.Should().Contain("strip chomping");
        }

        [Fact]
        public void IndentedFirstLineIsLiteralWithIndentationIndicator()
        {
            var decision = StringStyleSelector.Select("  indented\nnormal");

            decision.Style.Should().Be(ScalarStyle.Literal);
            decision.Reason.Should().Contain("indentation indicator");
        }

        [Fact]
        public void LinesEndingWithSpacesAreDoubleQuoted()
        {
            var decision = StringStyleSelector.Select("first \nsecond");

            decision.Style.Should().Be(ScalarStyle.DoubleQuoted);
            decision.Reason.Should().Contain("space");
        }

        [Fact]
        public void CarriageReturnsAreDoubleQuoted()
        {
            var decision = StringStyleSelector.Select("first\r\nsecond");

            decision.Style.Should().Be(ScalarStyle.DoubleQuoted);
            decision.Reason.Should().Contain("carriage return");
        }

        [Fact]
        public void SpecialCharactersAreDoubleQuoted()
        {
            var decision = StringStyleSelector.Select("bell\a");

            decision.Style.Should().Be(ScalarStyle.DoubleQuoted);
            decision.Reason.Should().Contain("not printable");
        }

        [Theory]
        [InlineData("yes")]
        [InlineData("no")]
        [InlineData("null")]
        [InlineData("~")]
        [InlineData("012")]
        [InlineData("0x1F")]
        [InlineData("1.5")]
        [InlineData("")]
        [InlineData("just some words")]
        [InlineData("line one\nline two\n\n")]
        [InlineData("line one\nline two\n")]
        [InlineData("line one\nline two")]
        [InlineData("  indented first line\nsecond line")]
        [InlineData("trailing space \nnext line")]
        [InlineData("key: value")]
        [InlineData("with # hash")]
        [InlineData("- dash")]
        [InlineData("2001-12-15")]
        public void EmittedStringsRoundTripVerbatim(string value)
        {
            var yaml = EmitScalarDocument(value, EmitterSettings.Default.WithStringStyleSelector(v => StringStyleSelector.Select(v)));

            ParseScalarDocument(yaml).Should().Be(value);
        }

        [Fact]
        public void CrlfOutputPreservesInternalLineFeeds()
        {
            const string value = "first line\nsecond line\n\n";
            var settings = EmitterSettings.Default
                .WithStringStyleSelector(v => StringStyleSelector.Select(v))
                .WithNewLine("\r\n");

            var yaml = EmitScalarDocument(value, settings);

            // The document itself uses CRLF...
            yaml.Should().Contain("\r\n");
            // ...but parsing it yields the original LF line breaks inside the scalar.
            ParseScalarDocument(yaml).Should().Be(value);
        }

        [Fact]
        public void CrlfOutputPreservesInternalLineFeedsInQuotedScalars()
        {
            const string value = "first \nsecond";
            var settings = EmitterSettings.Default
                .WithStringStyleSelector(v => StringStyleSelector.Select(v))
                .WithNewLine("\r\n");

            var yaml = EmitScalarDocument(value, settings);

            ParseScalarDocument(yaml).Should().Be(value);
        }

        [Fact]
        public void EmittedYesIsSingleQuoted()
        {
            var yaml = EmitScalarDocument("yes", EmitterSettings.Default.WithStringStyleSelector(v => StringStyleSelector.Select(v)));

            yaml.Trim().Should().Be("'yes'");
        }

        [Fact]
        public void EmittedTrailingLineBreaksUseKeepChomping()
        {
            var yaml = EmitScalarDocument("line one\nline two\n\n", EmitterSettings.Default.WithStringStyleSelector(v => StringStyleSelector.Select(v)));

            yaml.Should().Contain("|+");
        }

        [Fact]
        public void EmittedIndentedFirstLineUsesIndentationIndicator()
        {
            var yaml = EmitScalarDocument("  indented\nnormal", EmitterSettings.Default.WithStringStyleSelector(v => StringStyleSelector.Select(v)));

            yaml.Should().Contain("|2");
        }

        [Fact]
        public void DeserializedObjectKeepsStringType()
        {
            var yaml = EmitScalarDocument("yes", EmitterSettings.Default.WithStringStyleSelector(v => StringStyleSelector.Select(v)));

            var deserializer = new DeserializerBuilder().Build();
            deserializer.Deserialize<object>(yaml).Should().Be("yes");
            deserializer.Deserialize<string>(yaml).Should().Be("yes");
        }

        [Fact]
        public void DecisionExposesStyleAndReason()
        {
            var decision = StringStyleSelector.Select("yes");

            decision.ToString().Should().Contain("SingleQuoted").And.Contain("boolean");
        }

        [Fact]
        public void CreateScalarAppliesTheSelectedStyle()
        {
            var scalar = StringStyleSelector.CreateScalar("012", StringScalarSchema.Yaml11, out var decision);

            scalar.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Style.Should().Be(ScalarStyle.SingleQuoted);
            decision.Reason.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void EmitterWithoutSelectorKeepsDefaultBehavior()
        {
            // The regular entry points are unchanged: without a selector, multiline
            // strings still use the folded style and "yes" is emitted plain.
            EmitScalarDocument("yes", EmitterSettings.Default).Trim().Should().Be("yes");
            EmitScalarDocument("a\nb", EmitterSettings.Default).Should().Contain(">");
        }

        private static string EmitScalarDocument(string value, EmitterSettings settings)
        {
            var writer = new StringWriter();
            var emitter = new Emitter(writer, settings);
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart());
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Any, true, true));
            emitter.Emit(new DocumentEnd(true));
            emitter.Emit(new StreamEnd());
            return writer.ToString();
        }

        private static string ParseScalarDocument(string yaml)
        {
            var parser = new Parser(new StringReader(yaml));
            parser.Consume<StreamStart>();
            parser.Consume<DocumentStart>();
            var scalar = parser.Consume<Scalar>();
            return scalar.Value;
        }
    }
}
