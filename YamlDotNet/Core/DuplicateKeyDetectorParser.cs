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
using System.Text;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// An <see cref="IParser"/> decorator that checks the event stream for duplicate mapping
    /// keys while forwarding every event to the caller unchanged. Each mapping gets its own
    /// scope, so identical keys in different mappings (for example, mappings inside a
    /// sequence) or in different documents do not conflict.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scalar keys are compared through an injectable <see cref="IScalarKeyNormalizer"/>
    /// (see <see cref="ScalarKeyNormalizers.String"/> and <see cref="ScalarKeyNormalizers.CoreSchema"/>).
    /// Collection keys are compared structurally, and alias keys are compared as the node
    /// they reference. Anchored nodes are canonicalized at most once, so the detector never
    /// expands the whole alias graph; a configurable node budget bounds the work performed
    /// per document.
    /// </para>
    /// <para>
    /// Keys that cannot be compared for equality (alias cycles, unresolved aliases, or keys
    /// seen after the node budget was exceeded) are reported as
    /// <see cref="DuplicateKeyDiagnosticKind.UnsupportedKey"/> diagnostics instead of being
    /// silently ignored.
    /// </para>
    /// <para>
    /// This decorator is opt-in: the plain <see cref="Parser"/> does not perform duplicate
    /// key detection and does not impose any node budget.
    /// </para>
    /// </remarks>
    public sealed class DuplicateKeyDetectorParser : IParser
    {
        private readonly IParser innerParser;
        private readonly IScalarKeyNormalizer keyNormalizer;
        private readonly int nodeBudget;
        private readonly Action<DuplicateKeyDiagnostic>? onDiagnostic;
        private readonly List<DuplicateKeyDiagnostic> diagnostics = [];

        private readonly List<MappingScope?> scopes = [];
        private readonly List<NodeBuilder> builders = [];
        private readonly Dictionary<string, AnchorForm> anchors = new(StringComparer.Ordinal);
        private int nodesCanonicalized;
        private bool disabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateKeyDetectorParser"/> class
        /// that compares keys by their exact string value.
        /// </summary>
        /// <param name="innerParser">The parser to wrap.</param>
        public DuplicateKeyDetectorParser(IParser innerParser)
            : this(innerParser, new DuplicateKeyDetectorOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateKeyDetectorParser"/> class.
        /// </summary>
        /// <param name="innerParser">The parser to wrap.</param>
        /// <param name="keyNormalizer">The normalizer used to compare scalar keys.</param>
        public DuplicateKeyDetectorParser(IParser innerParser, IScalarKeyNormalizer keyNormalizer)
            : this(innerParser, new DuplicateKeyDetectorOptions { KeyNormalizer = keyNormalizer })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateKeyDetectorParser"/> class.
        /// </summary>
        /// <param name="innerParser">The parser to wrap.</param>
        /// <param name="options">The options that configure duplicate key detection.</param>
        public DuplicateKeyDetectorParser(IParser innerParser, DuplicateKeyDetectorOptions options)
        {
            this.innerParser = innerParser ?? throw new ArgumentNullException(nameof(innerParser));
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.keyNormalizer = options.KeyNormalizer ?? throw new ArgumentException("The key normalizer must not be null.", nameof(options));
            this.nodeBudget = options.NodeBudget;
            this.onDiagnostic = options.OnDiagnostic;
        }

        /// <summary>
        /// Gets the current event. Returns null before the first call to <see cref="MoveNext" />,
        /// and also after <see cref="MoveNext" /> returns false.
        /// </summary>
        public ParsingEvent? Current => this.innerParser.Current;

        /// <summary>
        /// Gets the diagnostics that have been reported so far.
        /// </summary>
        public IReadOnlyList<DuplicateKeyDiagnostic> Diagnostics => this.diagnostics;

        /// <summary>
        /// Moves to the next event, forwarding it unchanged, and updates the duplicate key
        /// detection state.
        /// </summary>
        /// <returns>Returns true if there are more events available, otherwise returns false.</returns>
        public bool MoveNext()
        {
            var moved = this.innerParser.MoveNext();
            if (moved)
            {
                ProcessEvent(this.innerParser.Current!);
            }

            return moved;
        }

        private void ProcessEvent(ParsingEvent parsingEvent)
        {
            switch (parsingEvent)
            {
                case StreamStart _:
                    ResetDocumentState();
                    break;
                case DocumentStart _:
                    ResetDocumentState();
                    break;
                case Scalar scalar:
                    HandleScalar(scalar);
                    break;
                case AnchorAlias alias:
                    HandleAlias(alias);
                    break;
                case MappingStart mappingStart:
                    HandleCollectionStart(mappingStart, isMapping: true);
                    break;
                case SequenceStart sequenceStart:
                    HandleCollectionStart(sequenceStart, isMapping: false);
                    break;
                case MappingEnd mappingEnd:
                    HandleCollectionEnd(mappingEnd);
                    break;
                case SequenceEnd sequenceEnd:
                    HandleCollectionEnd(sequenceEnd);
                    break;
            }
        }

        private void ResetDocumentState()
        {
            this.scopes.Clear();
            this.builders.Clear();
            this.anchors.Clear();
            this.nodesCanonicalized = 0;
            this.disabled = false;
        }

        private void HandleScalar(Scalar scalar)
        {
            var isKey = IsAtKeyPosition(out var keyScope);
            var tracked = this.builders.Count > 0 || isKey || !scalar.Anchor.IsEmpty;
            if (tracked && TryConsumeBudget(scalar))
            {
                var form = "S:" + this.keyNormalizer.NormalizeKey(scalar);
                CompleteNode(scalar.Anchor, form, unsupported: false, isKey, keyScope, scalar.Start, scalar.End);
            }

            ToggleKeyExpectation();
        }

        private void HandleAlias(AnchorAlias alias)
        {
            var isKey = IsAtKeyPosition(out var keyScope);
            var tracked = this.builders.Count > 0 || isKey;
            if (tracked && TryConsumeBudget(alias))
            {
                string form;
                bool unsupported;
                if (this.anchors.TryGetValue(alias.Value.Value, out var anchorForm))
                {
                    // Alias keys are compared as the node they reference. The anchored node
                    // was canonicalized once, so the alias graph is never re-expanded.
                    form = anchorForm.Form;
                    unsupported = anchorForm.Unsupported;
                }
                else
                {
                    // The anchor is either still being built (an alias cycle) or unknown.
                    // Equality cannot be determined, so the key must not be silently ignored.
                    form = "A:" + alias.Value.Value;
                    unsupported = true;
                }

                CompleteNode(AnchorName.Empty, form, unsupported, isKey, keyScope, alias.Start, alias.End);
            }

            ToggleKeyExpectation();
        }

        private void HandleCollectionStart(NodeEvent node, bool isMapping)
        {
            var isKey = IsAtKeyPosition(out var keyScope);
            ToggleKeyExpectation();
            this.scopes.Add(isMapping ? new MappingScope() : null);

            var tracked = this.builders.Count > 0 || isKey || !node.Anchor.IsEmpty;
            if (tracked && TryConsumeBudget(node))
            {
                var builder = new NodeBuilder
                {
                    Anchor = node.Anchor,
                    IsMapping = isMapping,
                    IsKey = isKey,
                    KeyScope = keyScope,
                    StartMark = node.Start,
                };
                builder.Text.Append(isMapping ? "M:[" : "Q:[");
                this.builders.Add(builder);
            }
        }

        private void HandleCollectionEnd(ParsingEvent parsingEvent)
        {
            this.scopes.RemoveAt(this.scopes.Count - 1);

            if (this.builders.Count > 0)
            {
                var lastIndex = this.builders.Count - 1;
                var builder = this.builders[lastIndex];
                this.builders.RemoveAt(lastIndex);
                builder.Text.Append(']');
                CompleteNode(builder.Anchor, builder.Text.ToString(), builder.Unsupported, builder.IsKey, builder.KeyScope, builder.StartMark, parsingEvent.End);
            }
        }

        private void CompleteNode(AnchorName anchor, string form, bool unsupported, bool isKey, MappingScope? keyScope, Mark start, Mark end)
        {
            if (this.builders.Count > 0)
            {
                var parent = this.builders[this.builders.Count - 1];
                if (parent.IsMapping && parent.ChildCount % 2 == 1)
                {
                    // Mapping children alternate between keys and values.
                    parent.Text.Append('=');
                }
                else if (parent.ChildCount > 0)
                {
                    parent.Text.Append(',');
                }

                parent.Text.Append(form);
                parent.ChildCount++;
                if (unsupported)
                {
                    parent.Unsupported = true;
                }
            }

            if (!anchor.IsEmpty)
            {
                this.anchors[anchor.Value] = new AnchorForm(form, unsupported);
            }

            if (isKey && keyScope != null)
            {
                CheckKey(keyScope, form, unsupported, start, end);
            }
        }

        private void CheckKey(MappingScope scope, string form, bool unsupported, Mark start, Mark end)
        {
            if (unsupported)
            {
                Emit(new DuplicateKeyDiagnostic(
                    DuplicateKeyDiagnosticKind.UnsupportedKey,
                    Mark.Empty,
                    Mark.Empty,
                    start,
                    end,
                    "The mapping key cannot be compared for equality because it contains an alias cycle or an unresolved alias."));
                return;
            }

            if (scope.Keys.TryGetValue(form, out var first))
            {
                Emit(new DuplicateKeyDiagnostic(
                    DuplicateKeyDiagnosticKind.DuplicateKey,
                    first.Start,
                    first.End,
                    start,
                    end,
                    "Duplicate mapping key."));
            }
            else
            {
                scope.Keys[form] = new KeyOccurrence(start, end);
            }
        }

        private bool TryConsumeBudget(ParsingEvent parsingEvent)
        {
            if (this.disabled)
            {
                return false;
            }

            this.nodesCanonicalized++;
            if (this.nodesCanonicalized > this.nodeBudget)
            {
                // Stop canonicalizing but keep the event stream and the scope stack consistent.
                this.disabled = true;
                this.builders.Clear();
                Emit(new DuplicateKeyDiagnostic(
                    DuplicateKeyDiagnosticKind.NodeBudgetExceeded,
                    Mark.Empty,
                    Mark.Empty,
                    parsingEvent.Start,
                    parsingEvent.End,
                    $"The node budget of {this.nodeBudget} was exceeded; duplicate key detection is disabled for the remainder of the document."));
                return false;
            }

            return true;
        }

        private bool IsAtKeyPosition(out MappingScope? keyScope)
        {
            if (this.scopes.Count > 0 && this.scopes[this.scopes.Count - 1] is MappingScope mapping && mapping.ExpectingKey)
            {
                keyScope = mapping;
                return true;
            }

            keyScope = null;
            return false;
        }

        private void ToggleKeyExpectation()
        {
            if (this.scopes.Count > 0 && this.scopes[this.scopes.Count - 1] is MappingScope mapping)
            {
                mapping.ExpectingKey = !mapping.ExpectingKey;
            }
        }

        private void Emit(DuplicateKeyDiagnostic diagnostic)
        {
            this.diagnostics.Add(diagnostic);
            this.onDiagnostic?.Invoke(diagnostic);
        }

        private sealed class MappingScope
        {
            public readonly Dictionary<string, KeyOccurrence> Keys = new Dictionary<string, KeyOccurrence>(StringComparer.Ordinal);
            public bool ExpectingKey = true;
        }

        private readonly struct KeyOccurrence
        {
            public readonly Mark Start;
            public readonly Mark End;

            public KeyOccurrence(Mark start, Mark end)
            {
                this.Start = start;
                this.End = end;
            }
        }

        private readonly struct AnchorForm
        {
            public readonly string Form;
            public readonly bool Unsupported;

            public AnchorForm(string form, bool unsupported)
            {
                this.Form = form;
                this.Unsupported = unsupported;
            }
        }

        private sealed class NodeBuilder
        {
            public readonly StringBuilder Text = new StringBuilder();
            public AnchorName Anchor;
            public bool IsMapping;
            public bool IsKey;
            public MappingScope? KeyScope;
            public Mark StartMark;
            public int ChildCount;
            public bool Unsupported;
        }
    }
}
