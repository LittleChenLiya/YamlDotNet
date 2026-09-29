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

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace YamlDotNet.Test.Serialization
{
    public class DocumentSelectionTests
    {
        private const string ThreeDocuments = """
            ---
            !counted
            Value: first
            ...
            ---
            Value: second
            ...
            ---
            !counted
            Value: third
            """;

        [Fact]
        public void SelectsOnlyTheRequestedDocument()
        {
            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<Sample>(ThreeDocuments, 1);

            result.Should().BeEquivalentTo(new Sample { Value = "second" });
        }

        [Fact]
        public void SkippedDocumentsDoNotInvokeCustomConvertersOrConstructObjects()
        {
            var converter = new CountingConverter();
            var deserializer = new DeserializerBuilder()
                .WithTagMapping("!counted", typeof(CountedModel))
                .WithTypeConverter(converter)
                .Build();

            var result = deserializer.Deserialize<Sample>(ThreeDocuments, 1);

            result.Value.Should().Be("second");
            converter.ReadCount.Should().Be(0);
            converter.ConstructedCount.Should().Be(0);
        }

        [Fact]
        public void SelectedDocumentWithTaggedTypeIsConstructedExactlyOnce()
        {
            var converter = new CountingConverter();
            var deserializer = new DeserializerBuilder()
                .WithTagMapping("!counted", typeof(CountedModel))
                .WithTypeConverter(converter)
                .Build();

            var result = deserializer.Deserialize<CountedModel>(ThreeDocuments, 2);

            result.Value.Should().Be("third");
            converter.ReadCount.Should().Be(1);
            converter.ConstructedCount.Should().Be(1);
        }

        [Fact]
        public void AliasesDoNotLeakFromSkippedDocumentsIntoTheSelectedOne()
        {
            var yaml = """
                ---
                &shared
                Value: anchored
                ...
                ---
                *shared
                """;
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(yaml, 1);

            act.Should().Throw<AnchorNotFoundException>();
        }

        [Fact]
        public void AnchorsInSelectedDocumentWorkNormally()
        {
            var yaml = """
                ---
                First: &shared
                    Value: anchored
                Second: *shared
                ...
                ---
                Value: other
                """;
            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<TwoSamples>(yaml, 0);

            result.First.Value.Should().Be("anchored");
            result.Second.Should().BeSameAs(result.First);
        }

        [Fact]
        public void EmptyDocumentsAreCountedAndDeserializeToNull()
        {
            var yaml = """
                ---
                ...
                ---
                Value: second
                ...
                ---
                """;
            var deserializer = new DeserializerBuilder().Build();

            deserializer.Deserialize<Sample>(yaml, 0).Should().BeNull();
            deserializer.Deserialize<Sample>(yaml, 1).Should().BeEquivalentTo(new Sample { Value = "second" });
            deserializer.Deserialize<Sample>(yaml, 2).Should().BeNull();
        }

        [Fact]
        public void LastDocumentDoesNotNeedAnEndMarker()
        {
            var yaml = """
                ---
                Value: first
                ...
                ---
                Value: second
                """;
            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<Sample>(yaml, 1);

            result.Value.Should().Be("second");
        }

        [Fact]
        public void SyntaxErrorsInDocumentsAfterTheSelectedOneAreStillReported()
        {
            var yaml = """
                ---
                Value: first
                ...
                ---
                Value: [unclosed
                """;
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(yaml, 0);

            act.Should().Throw<YamlException>();
        }

        [Fact]
        public void SyntaxErrorsInDocumentsBeforeTheSelectedOneAreStillReported()
        {
            var yaml = """
                ---
                Value: [unclosed
                ...
                ---
                Value: second
                """;
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(yaml, 1);

            act.Should().Throw<YamlException>();
        }

        [Fact]
        public void ConsumesTheWholeStreamAndEndsAtTheFinalPosition()
        {
            var parser = new RecordingParser(new Parser(new StringReader(ThreeDocuments)));
            var deserializer = new DeserializerBuilder().Build();

            deserializer.Deserialize<Sample>(parser, 1);

            parser.Events.Should().NotBeEmpty();
            parser.Events[^1].Should().BeOfType<StreamEnd>();
            parser.Events[^1].End.Index.Should().Be(ThreeDocuments.Length);
            parser.Current.Should().BeNull();
        }

        [Fact]
        public void TextReaderIsDisposedExactlyOnceAndNotBeforeDeserializationCompletes()
        {
            var reader = new TrackingTextReader(ThreeDocuments);
            var converter = new CountingConverter(() => reader.DisposeCount);
            var deserializer = new DeserializerBuilder()
                .WithTagMapping("!counted", typeof(CountedModel))
                .WithTypeConverter(converter)
                .Build();

            var result = deserializer.Deserialize<CountedModel>(reader, 2);

            result.Value.Should().Be("third");
            converter.ObservedDisposeCounts.Should().AllBeEquivalentTo(0);
            reader.DisposeCount.Should().Be(1);
        }

        [Fact]
        public void TextReaderIsDisposedExactlyOnceWhenParsingFails()
        {
            var yaml = """
                ---
                Value: first
                ...
                ---
                Value: [unclosed
                """;
            var reader = new TrackingTextReader(yaml);
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(reader, 0);

            act.Should().Throw<YamlException>();
            reader.DisposeCount.Should().Be(1);
        }

        [Fact]
        public void NegativeDocumentIndexThrows()
        {
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(ThreeDocuments, -1);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void DocumentIndexBeyondTheEndOfTheStreamThrows()
        {
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(ThreeDocuments, 3);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        private sealed class Sample
        {
            public string? Value { get; set; }
        }

        private sealed class TwoSamples
        {
            public Sample First { get; set; } = new Sample();
            public Sample Second { get; set; } = new Sample();
        }

        private sealed class CountedModel
        {
            public string? Value { get; set; }
        }

        private sealed class CountingConverter : IYamlTypeConverter
        {
            private readonly Func<int>? disposeCountProvider;

            public int ReadCount { get; private set; }
            public int ConstructedCount { get; private set; }
            public List<int> ObservedDisposeCounts { get; } = new List<int>();

            public CountingConverter(Func<int>? disposeCountProvider = null)
            {
                this.disposeCountProvider = disposeCountProvider;
            }

            public bool Accepts(Type type) => type == typeof(CountedModel);

            public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
            {
                ReadCount++;
                if (disposeCountProvider != null)
                {
                    ObservedDisposeCounts.Add(disposeCountProvider());
                }

                parser.Consume<MappingStart>();
                string? value = null;
                while (parser.TryConsume<Scalar>(out var key))
                {
                    if (key.Value == "Value")
                    {
                        value = parser.Consume<Scalar>().Value;
                    }
                    else
                    {
                        parser.SkipThisAndNestedEvents();
                    }
                }
                parser.Consume<MappingEnd>();

                ConstructedCount++;
                return new CountedModel { Value = value };
            }

            public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TrackingTextReader : StringReader
        {
            public int DisposeCount { get; private set; }

            public TrackingTextReader(string s) : base(s)
            {
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
                base.Dispose(disposing);
            }
        }

        private sealed class RecordingParser : IParser
        {
            private readonly IParser inner;

            public List<ParsingEvent> Events { get; } = new List<ParsingEvent>();

            public RecordingParser(IParser inner)
            {
                this.inner = inner;
            }

            public ParsingEvent? Current => inner.Current;

            public bool MoveNext()
            {
                var moved = inner.MoveNext();
                if (moved && inner.Current != null)
                {
                    Events.Add(inner.Current);
                }
                return moved;
            }
        }
    }
}
