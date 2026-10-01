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
using System.Globalization;
using System.Threading;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Test.Core
{
    public class DuplicateKeyCheckingParserTests
    {
        [Fact]
        public void StringMode_DistinguishesValuesThatDifferAsStrings()
        {
            var diagnostics = Check("1: a\n01: b", ScalarKeyNormalizers.Ordinal);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void StringMode_DetectsExactDuplicatesWithPositions()
        {
            var diagnostics = Check("1: a\n1: b", ScalarKeyNormalizers.Ordinal);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            diagnostic.FirstKeyStart.Line.Should().Be(1);
            diagnostic.FirstKeyStart.Column.Should().Be(1);
            diagnostic.FirstKeyEnd.Column.Should().Be(2);
            diagnostic.KeyStart.Line.Should().Be(2);
            diagnostic.KeyStart.Column.Should().Be(1);
            diagnostic.KeyEnd.Column.Should().Be(2);
        }

        [Fact]
        public void CoreSchemaMode_DetectsNumericallyEqualKeysWithPositions()
        {
            var diagnostics = Check("1: a\n01: b", ScalarKeyNormalizers.CoreSchema);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            diagnostic.FirstKeyStart.Line.Should().Be(1);
            diagnostic.FirstKeyStart.Column.Should().Be(1);
            diagnostic.FirstKeyEnd.Column.Should().Be(2);
            diagnostic.KeyStart.Line.Should().Be(2);
            diagnostic.KeyStart.Column.Should().Be(1);
            diagnostic.KeyEnd.Column.Should().Be(3);
        }

        [Fact]
        public void CoreSchemaMode_ExplicitStringTagIsNotEqualToInteger()
        {
            var diagnostics = Check("1: a\n!!str 1: b", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaMode_ExplicitStringTagsAreEqualToEachOther()
        {
            var diagnostics = Check("!!str 1: a\n!!str 1: b", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void StringMode_ExplicitStringTagIsEqualToPlainScalar()
        {
            var diagnostics = Check("!!str 1: a\n1: b", ScalarKeyNormalizers.Ordinal);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Theory]
        [InlineData("0x10: a\n16: b")]
        [InlineData("0o17: a\n15: b")]
        [InlineData("1.5: a\n1.50: b")]
        [InlineData("1e3: a\n1000.0: b")]
        [InlineData("true: a\nTRUE: b")]
        [InlineData("null: a\n~: b")]
        public void CoreSchemaMode_ResolvesScalarsBeforeComparing(string yaml)
        {
            var diagnostics = Check(yaml, ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void CoreSchemaMode_IntAndFloatAreDifferentKinds()
        {
            var diagnostics = Check("1: a\n1.0: b", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaMode_YesIsNotABool()
        {
            // "yes" is not a bool in the core schema, so both keys are plain equal strings.
            var diagnostics = Check("yes: a\nyes: b", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void CoreSchemaMode_NumberParsingIsCultureInvariant()
        {
            var previousCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                // de-DE uses a comma as decimal separator, which must not affect parsing.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var diagnostics = Check("1.5: a\n1.50: b", ScalarKeyNormalizers.CoreSchema);

                diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }

        [Fact]
        public void MultipleDocuments_DoNotShareScopes()
        {
            var diagnostics = Check("key: 1\n---\nkey: 2", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void NestedMappings_HaveTheirOwnScopes()
        {
            var diagnostics = Check("outer:\n  key: 1\nkey: 2", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void NestedMappings_DetectTheirOwnDuplicates()
        {
            var diagnostics = Check("outer:\n  key: 1\n  key: 2", ScalarKeyNormalizers.CoreSchema);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.FirstKeyStart.Line.Should().Be(2);
            diagnostic.KeyStart.Line.Should().Be(3);
        }

        [Fact]
        public void MappingsInsideASequence_DoNotConflict()
        {
            var diagnostics = Check("- key: 1\n- key: 2", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void AliasKeys_AreComparedByReferencedNode()
        {
            var diagnostics = Check("&k key: 1\n*k : 2", ScalarKeyNormalizers.CoreSchema);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            diagnostic.FirstKeyStart.Line.Should().Be(1);
            diagnostic.KeyStart.Line.Should().Be(2);
        }

        [Fact]
        public void AliasKeys_AreEqualToEquivalentLiteralKeys()
        {
            var diagnostics = Check("&s [1, 2]: a\n[1, 2]: b", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void SequenceKeys_AreComparedStructurally()
        {
            var diagnostics = Check("[1, 2]: a\n[1, 2]: b\n[1, 3]: c", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void AliasCycles_AreReportedAsUnsupported()
        {
            var diagnostics = Check("? &s [*s]\n: value", ScalarKeyNormalizers.CoreSchema);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedAliasCycle);
            diagnostic.KeyStart.Line.Should().Be(1);
        }

        [Fact]
        public void UnknownAnchors_AreReportedAsUnsupported()
        {
            var diagnostics = Check("*nope : 1", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedUnknownAnchor);
        }

        [Fact]
        public void MappingKeys_AreReportedAsUnsupported()
        {
            var diagnostics = Check("? {a: 1}\n: 2", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedMappingKey);
        }

        [Fact]
        public void KeysExceedingTheNodeBudget_AreReportedAsUnsupported()
        {
            var parser = CreateParser("[a, b, c]: 1", ScalarKeyNormalizers.CoreSchema, maxKeyNodes: 3);
            Drain(parser);

            parser.Diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedNodeBudgetExceeded);
        }

        [Fact]
        public void CustomNormalizers_CanBeInjected()
        {
            var diagnostics = Check("Key: 1\nkey: 2", new CaseInsensitiveNormalizer());

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void DiagnosticsAreRaisedThroughTheCallback()
        {
            var raised = new List<DuplicateKeyDiagnostic>();
            var parser = CreateParser("1: a\n01: b", ScalarKeyNormalizers.CoreSchema, onDiagnostic: raised.Add);
            Drain(parser);

            raised.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            parser.Diagnostics.Should().BeEquivalentTo(raised);
        }

        [Fact]
        public void EventsPassThroughUnmodified()
        {
            var inner = Yaml.ParserForText("1: a\n01: b");
            var parser = new DuplicateKeyCheckingParser(inner, ScalarKeyNormalizers.CoreSchema);

            while (parser.MoveNext())
            {
                parser.Current.Should().BeSameAs(inner.Current);
            }
        }

        [Fact]
        public void EventFixture_DetectsDuplicatesWithoutParsingText()
        {
            // Builds the event stream directly, demonstrating that the checker operates
            // purely at the event level and can be fed from any event source.
            var events = new ParsingEvent[]
            {
                new StreamStart(),
                new DocumentStart(default, default, true),
                new MappingStart(AnchorName.Empty, TagName.Empty, true, MappingStyle.Block),
                KeyScalar("1"),
                new Scalar("a"),
                KeyScalar("01"),
                new Scalar("b"),
                new MappingEnd(),
                new DocumentEnd(true),
                new StreamEnd(),
            };

            var parser = new DuplicateKeyCheckingParser(new EventFixtureParser(events), ScalarKeyNormalizers.CoreSchema);
            Drain(parser);

            parser.Diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void EventFixture_MappingsInSequenceDoNotConflict()
        {
            var events = new ParsingEvent[]
            {
                new StreamStart(),
                new DocumentStart(default, default, true),
                new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Block),
                new MappingStart(AnchorName.Empty, TagName.Empty, true, MappingStyle.Block),
                KeyScalar("key"),
                new Scalar("1"),
                new MappingEnd(),
                new MappingStart(AnchorName.Empty, TagName.Empty, true, MappingStyle.Block),
                KeyScalar("key"),
                new Scalar("2"),
                new MappingEnd(),
                new SequenceEnd(),
                new DocumentEnd(true),
                new StreamEnd(),
            };

            var parser = new DuplicateKeyCheckingParser(new EventFixtureParser(events), ScalarKeyNormalizers.CoreSchema);
            Drain(parser);

            parser.Diagnostics.Should().BeEmpty();
        }

        private static Scalar KeyScalar(string value)
        {
            return new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Plain, true, true, Mark.Empty, Mark.Empty, true);
        }

        private static List<DuplicateKeyDiagnostic> Check(string yaml, IScalarKeyNormalizer normalizer)
        {
            var parser = CreateParser(yaml, normalizer);
            Drain(parser);
            return new List<DuplicateKeyDiagnostic>(parser.Diagnostics);
        }

        private static DuplicateKeyCheckingParser CreateParser(
            string yaml,
            IScalarKeyNormalizer normalizer,
            int maxKeyNodes = DuplicateKeyCheckingParser.DefaultMaxKeyNodes,
            Action<DuplicateKeyDiagnostic> onDiagnostic = null)
        {
            return new DuplicateKeyCheckingParser(Yaml.ParserForText(yaml), normalizer, maxKeyNodes, onDiagnostic);
        }

        private static void Drain(IParser parser)
        {
            while (parser.MoveNext())
            {
            }
        }

        private sealed class CaseInsensitiveNormalizer : IScalarKeyNormalizer
        {
            public NormalizedScalarKey Normalize(Scalar scalar)
            {
                return new NormalizedScalarKey("str", scalar.Value.ToUpperInvariant());
            }
        }

        private sealed class EventFixtureParser : IParser
        {
            private readonly Queue<ParsingEvent> events;

            public EventFixtureParser(IEnumerable<ParsingEvent> events)
            {
                this.events = new Queue<ParsingEvent>(events);
            }

            public ParsingEvent Current { get; private set; }

            public bool MoveNext()
            {
                if (events.Count == 0)
                {
                    Current = null;
                    return false;
                }

                Current = events.Dequeue();
                return true;
            }
        }
    }
}
