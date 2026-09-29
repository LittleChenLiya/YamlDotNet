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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

#nullable enable

namespace YamlDotNet.Test.Serialization
{
    public class DocumentSelectionTests
    {
        [Fact]
        public void DeserializesOnlyTheSelectedDocument()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: first",
                "---",
                "Name: second",
                "---",
                "Name: third",
                "");

            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<Sample>(yaml, 1);

            result.Name.Should().Be("second");
        }

        [Fact]
        public void SkippedDocumentsDoNotTriggerObjectConstructionOrCustomConverters()
        {
            var yaml = string.Join("\n",
                "---",
                "!counted first",
                "---",
                "Name: selected",
                "---",
                "!counted second",
                "");

            var converter = new CountingConverter();
            var objectFactory = new CountingObjectFactory();
            var deserializer = new DeserializerBuilder()
                .WithTagMapping("!counted", typeof(Counted))
                .WithTypeConverter(converter)
                .WithObjectFactory(objectFactory)
                .Build();

            var result = deserializer.Deserialize<Sample>(yaml, 1);

            result.Name.Should().Be("selected");
            converter.ReadCount.Should().Be(0);
            objectFactory.CountedCreateCount.Should().Be(0);
        }

        [Fact]
        public void AnchorNamesCanBeReusedAcrossDocumentsBecauseAliasStateIsNotShared()
        {
            var yaml = string.Join("\n",
                "---",
                "&shared skipped",
                "---",
                "first: &shared selected",
                "second: *shared",
                "");

            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<Dictionary<string, string>>(yaml, 1);

            result.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                { "first", "selected" },
                { "second", "selected" },
            });
        }

        [Fact]
        public void AliasesDefinedInSkippedDocumentsAreNotVisibleToTheSelectedDocument()
        {
            var yaml = string.Join("\n",
                "---",
                "&skipped value",
                "---",
                "*skipped",
                "");

            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<object>(yaml, 1);

            act.Should().Throw<AnchorNotFoundException>();
        }

        [Fact]
        public void SelectingAnEmptyDocumentReturnsNull()
        {
            var yaml = string.Join("\n",
                "---",
                "---",
                "value: 42",
                "");

            var deserializer = new DeserializerBuilder().Build();

            deserializer.Deserialize<object>(yaml, 0).Should().BeNull();
            deserializer.Deserialize<Dictionary<string, int>>(yaml, 1)
                .Should().BeEquivalentTo(new Dictionary<string, int> { { "value", 42 } });
        }

        [Fact]
        public void LastDocumentDoesNotNeedAnEndMarker()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: first",
                "...",
                "---",
                "Name: second");

            var deserializer = new DeserializerBuilder().Build();

            var result = deserializer.Deserialize<Sample>(yaml, 1);

            result.Name.Should().Be("second");
        }

        [Fact]
        public void InvalidSyntaxAfterTheSelectedDocumentIsReported()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: selected",
                "---",
                "[unclosed",
                "");

            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(yaml, 0);

            act.Should().Throw<YamlException>();
        }

        [Fact]
        public void InvalidSyntaxInASkippedDocumentBeforeTheSelectedOneIsReported()
        {
            var yaml = string.Join("\n",
                "---",
                "[unclosed",
                "---",
                "Name: selected",
                "");

            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<Sample>(yaml, 1);

            act.Should().Throw<YamlException>();
        }

        [Fact]
        public void ReadsTheWholeStreamWithoutDisposingTheReader()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: first",
                "---",
                "Name: second",
                "---",
                "Name: third",
                "");

            var deserializer = new DeserializerBuilder().Build();
            var reader = new TrackingTextReader(yaml);

            var result = deserializer.Deserialize<Sample>(reader, 0);

            result.Name.Should().Be("first");
            reader.CharactersRead.Should().Be(yaml.Length, "every document must be parsed, even the ones after the selected one");
            reader.DisposeCount.Should().Be(0, "the deserializer must not release a reader that it does not own");

            reader.Dispose();
            reader.DisposeCount.Should().Be(1, "the reader must be released exactly once by its owner");
        }

        [Fact]
        public void DoesNotDisposeTheReaderWhenAnErrorOccurs()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: selected",
                "---",
                "[unclosed",
                "");

            var deserializer = new DeserializerBuilder().Build();
            var reader = new TrackingTextReader(yaml);

            Action act = () => deserializer.Deserialize<Sample>(reader, 0);

            act.Should().Throw<YamlException>();
            reader.DisposeCount.Should().Be(0, "the deserializer must not release a reader that it does not own");

            reader.Dispose();
            reader.DisposeCount.Should().Be(1, "the reader must be released exactly once by its owner");
        }

        [Fact]
        public void SelectingADocumentBeyondTheEndOfTheStreamReturnsNull()
        {
            var yaml = string.Join("\n",
                "---",
                "Name: only",
                "");

            var deserializer = new DeserializerBuilder().Build();

            deserializer.Deserialize<object>(yaml, 5).Should().BeNull();
        }

        [Fact]
        public void NegativeDocumentIndexIsRejected()
        {
            var deserializer = new DeserializerBuilder().Build();

            Action act = () => deserializer.Deserialize<object>("value: 1", -1);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        public class Sample
        {
            public string? Name { get; set; }
        }

        public class Counted
        {
        }

        private sealed class CountingConverter : IYamlTypeConverter
        {
            public int ReadCount { get; private set; }

            public bool Accepts(Type type) => type == typeof(Counted);

            public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
            {
                ReadCount++;
                parser.Consume<Scalar>();
                return new Counted();
            }

            public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class CountingObjectFactory : IObjectFactory
        {
            private readonly IObjectFactory inner = new DefaultObjectFactory();

            public int CountedCreateCount { get; private set; }

            public object Create(Type type)
            {
                if (type == typeof(Counted))
                {
                    CountedCreateCount++;
                }
                return inner.Create(type);
            }

            public object? CreatePrimitive(Type type) => inner.CreatePrimitive(type);

            public bool GetDictionary(IObjectDescriptor descriptor, out IDictionary? dictionary, out Type[]? genericArguments)
                => inner.GetDictionary(descriptor, out dictionary, out genericArguments);

            public Type GetValueType(Type type) => inner.GetValueType(type);

            public void ExecuteOnDeserializing(object value) => inner.ExecuteOnDeserializing(value);

            public void ExecuteOnDeserialized(object value) => inner.ExecuteOnDeserialized(value);

            public void ExecuteOnSerializing(object value) => inner.ExecuteOnSerializing(value);

            public void ExecuteOnSerialized(object value) => inner.ExecuteOnSerialized(value);
        }

        private sealed class TrackingTextReader : StringReader
        {
            public int CharactersRead { get; private set; }
            public int DisposeCount { get; private set; }

            public TrackingTextReader(string input)
                : base(input)
            {
            }

            public override int Read()
            {
                var result = base.Read();
                if (result >= 0)
                {
                    CharactersRead++;
                }
                return result;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                var read = base.Read(buffer, index, count);
                CharactersRead += read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCount++;
                }
                base.Dispose(disposing);
            }
        }
    }
}
