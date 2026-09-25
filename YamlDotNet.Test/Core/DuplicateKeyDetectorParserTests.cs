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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Test.Core
{
    public class DuplicateKeyDetectorParserTests : EventsHelper
    {
        [Fact]
        public void StringModeDistinguishesNumbersWithLeadingZeros()
        {
            var diagnostics = GetDiagnostics(@"
                1: a
                01: b
            ");

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaModeDetectsNumericallyEqualKeys()
        {
            var diagnostics = GetDiagnostics(@"
                1: a
                01: b
            ", ScalarKeyNormalizers.CoreSchema);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            AssertMark(diagnostic.FirstKeyStart, line: 1, column: 1);
            AssertMark(diagnostic.FirstKeyEnd, line: 1, column: 2);
            AssertMark(diagnostic.KeyStart, line: 2, column: 1);
            AssertMark(diagnostic.KeyEnd, line: 2, column: 3);
        }

        [Fact]
        public void CoreSchemaModeTreatsExplicitStringTagAsString()
        {
            var diagnostics = GetDiagnostics(@"
                1: a
                !!str 1: b
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaModeDetectsDuplicateExplicitlyTaggedStrings()
        {
            var diagnostics = GetDiagnostics(@"
                !!str 1: a
                !!str 1: b
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void CoreSchemaModeTreatsQuotedScalarsAsStrings()
        {
            var diagnostics = GetDiagnostics(@"
                1: a
                '1': b
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaModeCanonicalizesIntegersAcrossBases()
        {
            var diagnostics = GetDiagnostics(@"
                16: a
                0x10: b
                0o20: c
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().HaveCount(2);
            diagnostics.Should().OnlyContain(d => d.Kind == DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void CoreSchemaModeKeepsIntegersAndFloatsDistinct()
        {
            var diagnostics = GetDiagnostics(@"
                1: a
                1.0: b
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void CoreSchemaModeResolvesNullAndBoolButNotYaml11Keywords()
        {
            var diagnostics = GetDiagnostics(@"
                null: a
                ~: b
                true: c
                TRUE: d
                yes: e
            ", ScalarKeyNormalizers.CoreSchema);

            diagnostics.Should().HaveCount(2);
            diagnostics.Should().OnlyContain(d => d.Kind == DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void CoreSchemaModeParsesNumbersUsingTheInvariantCulture()
        {
            var previousCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                var diagnostics = GetDiagnostics(@"
                    1.5: a
                    15: b
                    1.50: c
                ", ScalarKeyNormalizers.CoreSchema);

                var diagnostic = diagnostics.Should().ContainSingle().Which;
                diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
                AssertMark(diagnostic.FirstKeyStart, line: 1, column: 1);
                AssertMark(diagnostic.KeyStart, line: 3, column: 1);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }

        [Fact]
        public void ReportsStartAndEndMarksOfFirstAndDuplicateKeys()
        {
            var diagnostics = GetDiagnostics(@"
                first: 1
                second: 2
                first: 3
            ");

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            AssertMark(diagnostic.FirstKeyStart, line: 1, column: 1);
            AssertMark(diagnostic.FirstKeyEnd, line: 1, column: 6);
            AssertMark(diagnostic.KeyStart, line: 3, column: 1);
            AssertMark(diagnostic.KeyEnd, line: 3, column: 6);
        }

        [Fact]
        public void MappingsInsideASequenceDoNotConflictWithEachOther()
        {
            var diagnostics = GetDiagnostics(@"
                - name: a
                  name: b
                - name: c
            ");

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            AssertMark(diagnostic.FirstKeyStart, line: 1, column: 3);
            AssertMark(diagnostic.KeyStart, line: 2, column: 3);
        }

        [Fact]
        public void NestedMappingsHaveTheirOwnScopes()
        {
            var diagnostics = GetDiagnostics(@"
                outer:
                  inner: 1
                  inner: 2
                sibling:
                  inner: 3
            ");

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            AssertMark(diagnostic.FirstKeyStart, line: 2, column: 3);
            AssertMark(diagnostic.KeyStart, line: 3, column: 3);
        }

        [Fact]
        public void MultipleDocumentsAreIsolated()
        {
            var diagnostics = GetDiagnostics(@"
                ---
                key: 1
                ...
                ---
                key: 2
            ");

            diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void AnchorsDoNotLeakAcrossDocuments()
        {
            var diagnostics = GetDiagnostics(@"
                ---
                &x thing: 1
                ...
                ---
                *x : 2
            ");

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedKey);
        }

        [Fact]
        public void AliasKeysAreComparedAsTheReferencedNode()
        {
            var diagnostics = GetDiagnostics(@"
                &a foo: 1
                *a : 2
            ");

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            AssertMark(diagnostic.FirstKeyStart, line: 1, column: 1);
            AssertMark(diagnostic.KeyStart, line: 2, column: 1);
        }

        [Fact]
        public void CollectionKeysAreComparedStructurally()
        {
            var diagnostics = GetDiagnostics(@"
                ? &a [1, 2]
                : first
                ? &b [1, 2]
                : second
            ");

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void AliasCycleInKeyReportsUnsupported()
        {
            var diagnostics = GetDiagnostics(@"
                ? &a [*a]
                : 1
            ");

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedKey);
            AssertMark(diagnostic.KeyStart, line: 1, column: 3);
        }

        [Fact]
        public void AliasToCyclicNodeReportsUnsupportedAgain()
        {
            var diagnostics = GetDiagnostics(@"
                ? &a [*a]
                : 1
                ? *a
                : 2
            ");

            diagnostics.Should().HaveCount(2);
            diagnostics.Should().OnlyContain(d => d.Kind == DuplicateKeyDiagnosticKind.UnsupportedKey);
        }

        [Fact]
        public void UnknownAliasKeyReportsUnsupported()
        {
            var diagnostics = GetDiagnostics(@"
                *unknown : 1
            ");

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.UnsupportedKey);
        }

        [Fact]
        public void ExceedingTheNodeBudgetReportsADiagnosticAndStopsDetection()
        {
            var diagnostics = GetDiagnostics(@"
                a: 1
                b: 2
                a: 3
            ", nodeBudget: 2);

            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Kind.Should().Be(DuplicateKeyDiagnosticKind.NodeBudgetExceeded);
            AssertMark(diagnostic.KeyStart, line: 3, column: 1);
        }

        [Fact]
        public void DiagnosticsAreAlsoReportedThroughTheCallback()
        {
            var reported = new List<DuplicateKeyDiagnostic>();
            var options = new DuplicateKeyDetectorOptions
            {
                OnDiagnostic = reported.Add,
            };

            var parser = new DuplicateKeyDetectorParser(Yaml.ParserForText("a: 1\na: 2"), options);
            while (parser.MoveNext())
            {
            }

            reported.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
            parser.Diagnostics.Should().BeEquivalentTo(reported);
        }

        [Fact]
        public void CustomNormalizerCanBeInjected()
        {
            var diagnostics = GetDiagnostics(@"
                Key: 1
                key: 2
            ", new UpperCaseNormalizer());

            diagnostics.Should().ContainSingle().Which.Kind.Should().Be(DuplicateKeyDiagnosticKind.DuplicateKey);
        }

        [Fact]
        public void ForwardsAllEventsUnchanged()
        {
            AssertSequenceOfEventsFrom(
                new DuplicateKeyDetectorParser(Yaml.ParserForText("a: 1\na: 2")),
                StreamStart,
                DocumentStart(Implicit),
                BlockMappingStart,
                PlainScalar("a").IsKey,
                PlainScalar("1"),
                PlainScalar("a").IsKey,
                PlainScalar("2"),
                MappingEnd,
                DocumentEnd(Implicit),
                StreamEnd);
        }

        private static List<DuplicateKeyDiagnostic> GetDiagnostics(string yaml, IScalarKeyNormalizer keyNormalizer = null, int? nodeBudget = null)
        {
            var options = new DuplicateKeyDetectorOptions();
            if (keyNormalizer != null)
            {
                options.KeyNormalizer = keyNormalizer;
            }

            if (nodeBudget.HasValue)
            {
                options.NodeBudget = nodeBudget.Value;
            }

            var parser = new DuplicateKeyDetectorParser(Yaml.ParserForText(yaml), options);
            while (parser.MoveNext())
            {
            }

            return parser.Diagnostics.ToList();
        }

        private static void AssertMark(Mark mark, long line, long column)
        {
            mark.Line.Should().Be(line);
            mark.Column.Should().Be(column);
        }

        private sealed class UpperCaseNormalizer : IScalarKeyNormalizer
        {
            public string NormalizeKey(Scalar scalar)
            {
                return "str:" + scalar.Value.ToUpperInvariant();
            }
        }
    }
}
