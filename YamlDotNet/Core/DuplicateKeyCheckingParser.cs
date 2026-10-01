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
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// An <see cref="IParser"/> decorator that detects duplicate mapping keys while streaming events.
    /// Each mapping gets its own scope, so identical keys in different mappings (for example the items
    /// of a sequence) or in different documents do not conflict. Alias keys are compared by the node
    /// they refer to. Keys that cannot be compared (mappings used as keys, alias cycles, unknown anchors
    /// or keys that exceed the node budget) are reported as unsupported diagnostics instead of being
    /// silently ignored.
    /// </summary>
    /// <remarks>
    /// The checker never expands the alias graph: anchored nodes are captured once, up to
    /// <see cref="DefaultMaxKeyNodes"/> nodes per key or anchored node, and aliases reuse the captured
    /// representation. The wrapped parser is not modified and keeps its own (unrestricted) behavior.
    /// Diagnostics are accumulated in <see cref="Diagnostics"/> and are also raised through the
    /// optional callback passed to the constructor as soon as they are detected.
    /// </remarks>
    public sealed class DuplicateKeyCheckingParser : IParser
    {
        /// <summary>
        /// The default maximum number of nodes that a single key or anchored node may contain.
        /// </summary>
        public const int DefaultMaxKeyNodes = 1024;

        private readonly IParser innerParser;
        private readonly IScalarKeyNormalizer keyNormalizer;
        private readonly int maxKeyNodes;
        private readonly Action<DuplicateKeyDiagnostic>? onDiagnostic;
        private readonly List<DuplicateKeyDiagnostic> diagnostics = new List<DuplicateKeyDiagnostic>();

        private readonly Stack<ContainerFrame> containers = new Stack<ContainerFrame>();
        private readonly Stack<NodeContext> contexts = new Stack<NodeContext>();
        private readonly Stack<NodeBuilder> builders = new Stack<NodeBuilder>();
        private readonly Dictionary<string, CapturedAnchor> anchors = new Dictionary<string, CapturedAnchor>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the diagnostics that have been detected so far.
        /// </summary>
        public IReadOnlyList<DuplicateKeyDiagnostic> Diagnostics => diagnostics;

        /// <inheritdoc />
        public ParsingEvent? Current => innerParser.Current;

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateKeyCheckingParser"/> class.
        /// </summary>
        /// <param name="innerParser">The parser to wrap.</param>
        /// <param name="keyNormalizer">
        /// The normalizer used to compare scalar keys. Defaults to <see cref="ScalarKeyNormalizers.CoreSchema"/>;
        /// use <see cref="ScalarKeyNormalizers.Ordinal"/> for plain ordinal string comparison, or provide a
        /// custom <see cref="IScalarKeyNormalizer"/>.
        /// </param>
        /// <param name="maxKeyNodes">The maximum number of nodes allowed in a single key or anchored node.</param>
        /// <param name="onDiagnostic">An optional callback that is invoked whenever a diagnostic is detected.</param>
        public DuplicateKeyCheckingParser(
            IParser innerParser,
            IScalarKeyNormalizer? keyNormalizer = null,
            int maxKeyNodes = DefaultMaxKeyNodes,
            Action<DuplicateKeyDiagnostic>? onDiagnostic = null)
        {
            this.innerParser = innerParser ?? throw new ArgumentNullException(nameof(innerParser));
            if (maxKeyNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxKeyNodes), "The node budget must be a positive integer.");
            }

            this.keyNormalizer = keyNormalizer ?? ScalarKeyNormalizers.CoreSchema;
            this.maxKeyNodes = maxKeyNodes;
            this.onDiagnostic = onDiagnostic;
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (!innerParser.MoveNext())
            {
                return false;
            }

            ProcessEvent(innerParser.Current!);
            return true;
        }

        private void ProcessEvent(ParsingEvent parsingEvent)
        {
            switch (parsingEvent)
            {
                case StreamStart _:
                    containers.Clear();
                    contexts.Clear();
                    builders.Clear();
                    anchors.Clear();
                    break;
                case DocumentStart _:
                    // Anchors are scoped to a single document.
                    anchors.Clear();
                    break;
                case Scalar scalar:
                    ProcessScalar(scalar);
                    break;
                case AnchorAlias alias:
                    ProcessAlias(alias);
                    break;
                case SequenceStart sequenceStart:
                    ProcessSequenceStart(sequenceStart);
                    break;
                case MappingStart mappingStart:
                    ProcessMappingStart(mappingStart);
                    break;
                case SequenceEnd sequenceEnd:
                    ProcessCollectionEnd(sequenceEnd, ContainerKind.Sequence);
                    break;
                case MappingEnd mappingEnd:
                    ProcessCollectionEnd(mappingEnd, ContainerKind.Mapping);
                    break;
            }
        }

        private NodeContext BeginNode()
        {
            var context = new NodeContext();
            if (containers.Count > 0 && containers.Peek().Mapping is { } scope)
            {
                if (scope.ExpectingKey)
                {
                    context.KeyOf = scope;
                }
                else
                {
                    context.ValueOf = scope;
                }
            }

            contexts.Push(context);

            if (builders.Count > 0)
            {
                var capture = builders.Peek().Capture;
                capture.NodeCount++;
                if (capture.NodeCount > maxKeyNodes && capture.Unsupported is null)
                {
                    capture.Unsupported = UnsupportedReason.NodeBudgetExceeded;
                }
            }

            return context;
        }

        private void ProcessScalar(Scalar scalar)
        {
            BeginNode();
            var node = new ScalarKeyNode(keyNormalizer.Normalize(scalar));
            if (!scalar.Anchor.IsEmpty)
            {
                anchors[scalar.Anchor.Value] = new ResolvedAnchor(node);
            }

            CompleteNode(node, null, scalar.Start, scalar.End);
        }

        private void ProcessAlias(AnchorAlias alias)
        {
            BeginNode();
            KeyNode? node = null;
            UnsupportedReason? unsupported = null;
            if (anchors.TryGetValue(alias.Value.Value, out var captured))
            {
                switch (captured)
                {
                    case ResolvedAnchor resolved:
                        node = resolved.Node;
                        break;
                    case InProgressAnchor _:
                        unsupported = UnsupportedReason.AliasCycle;
                        break;
                    case UnsupportedAnchor unsupportedAnchor:
                        unsupported = unsupportedAnchor.Reason;
                        break;
                }
            }
            else
            {
                unsupported = UnsupportedReason.UnknownAnchor;
            }

            CompleteNode(node, unsupported, alias.Start, alias.End);
        }

        private void ProcessSequenceStart(SequenceStart sequenceStart)
        {
            var context = BeginNode();
            containers.Push(new ContainerFrame(null));
            BeginCaptureIfNeeded(sequenceStart.Anchor, sequenceStart.Start, context, ContainerKind.Sequence);
        }

        private void ProcessMappingStart(MappingStart mappingStart)
        {
            var context = BeginNode();
            containers.Push(new ContainerFrame(new MappingScope()));
            BeginCaptureIfNeeded(mappingStart.Anchor, mappingStart.Start, context, ContainerKind.Mapping);
        }

        private void BeginCaptureIfNeeded(AnchorName anchor, Mark start, NodeContext context, ContainerKind kind)
        {
            if (builders.Count == 0 && anchor.IsEmpty && context.KeyOf is null)
            {
                // This node is neither anchored nor a key, and is not part of a captured node.
                return;
            }

            var capture = builders.Count > 0 ? builders.Peek().Capture : new CaptureState { NodeCount = 1 };
            builders.Push(kind == ContainerKind.Sequence
                ? (NodeBuilder)new SequenceKeyBuilder(anchor, start, capture)
                : new MappingKeyBuilder(anchor, start, capture));

            if (!anchor.IsEmpty)
            {
                anchors[anchor.Value] = InProgressAnchor.Instance;
            }
        }

        private void ProcessCollectionEnd(ParsingEvent endEvent, ContainerKind kind)
        {
            containers.Pop();

            var node = default(KeyNode);
            var unsupported = default(UnsupportedReason?);
            var start = endEvent.Start;

            if (builders.Count > 0 && builders.Peek().Kind == kind)
            {
                var builder = builders.Pop();
                start = builder.Start;
                unsupported = builder.Capture.Unsupported;
                if (kind == ContainerKind.Mapping)
                {
                    // Mappings used as keys cannot be compared for equality.
                    unsupported ??= UnsupportedReason.MappingKey;
                }

                if (unsupported is null)
                {
                    node = builder.Build();
                }

                if (!builder.Anchor.IsEmpty)
                {
                    anchors[builder.Anchor.Value] = node is null
                        ? (CapturedAnchor)new UnsupportedAnchor(unsupported!.Value)
                        : new ResolvedAnchor(node);
                }
            }

            CompleteNode(node, unsupported, start, endEvent.End);
        }

        private void CompleteNode(KeyNode? node, UnsupportedReason? unsupported, Mark start, Mark end)
        {
            var context = contexts.Pop();

            if (builders.Count > 0)
            {
                builders.Peek().AddChild(node, unsupported);
            }

            if (context.KeyOf is { } keyScope)
            {
                keyScope.ExpectingKey = false;
                CheckKey(keyScope, node, unsupported, start, end);
            }
            else if (context.ValueOf is { } valueScope)
            {
                valueScope.ExpectingKey = true;
            }
        }

        private void CheckKey(MappingScope scope, KeyNode? node, UnsupportedReason? unsupported, Mark start, Mark end)
        {
            if (unsupported is { } reason)
            {
                Emit(new DuplicateKeyDiagnostic(
                    ToDiagnosticKind(reason),
                    Mark.Empty,
                    Mark.Empty,
                    start,
                    end,
                    UnsupportedMessage(reason)));
                return;
            }

            if (scope.SeenKeys.TryGetValue(node!, out var first))
            {
                Emit(new DuplicateKeyDiagnostic(
                    DuplicateKeyDiagnosticKind.DuplicateKey,
                    first.Start,
                    first.End,
                    start,
                    end,
                    "A key with the same value was already used in this mapping."));
            }
            else
            {
                scope.SeenKeys.Add(node!, new KeyOccurrence(start, end));
            }
        }

        private void Emit(DuplicateKeyDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
            onDiagnostic?.Invoke(diagnostic);
        }

        private static DuplicateKeyDiagnosticKind ToDiagnosticKind(UnsupportedReason reason)
        {
            switch (reason)
            {
                case UnsupportedReason.MappingKey: return DuplicateKeyDiagnosticKind.UnsupportedMappingKey;
                case UnsupportedReason.AliasCycle: return DuplicateKeyDiagnosticKind.UnsupportedAliasCycle;
                case UnsupportedReason.UnknownAnchor: return DuplicateKeyDiagnosticKind.UnsupportedUnknownAnchor;
                default: return DuplicateKeyDiagnosticKind.UnsupportedNodeBudgetExceeded;
            }
        }

        private static string UnsupportedMessage(UnsupportedReason reason)
        {
            switch (reason)
            {
                case UnsupportedReason.MappingKey:
                    return "Mappings used as keys cannot be compared for equality.";
                case UnsupportedReason.AliasCycle:
                    return "The key contains an alias that refers to the anchor being defined, forming a cycle.";
                case UnsupportedReason.UnknownAnchor:
                    return "The key contains an alias that refers to an unknown anchor.";
                default:
                    return "The key contains more nodes than the configured node budget allows.";
            }
        }

        private enum ContainerKind
        {
            Sequence,
            Mapping,
        }

        private enum UnsupportedReason
        {
            MappingKey,
            AliasCycle,
            UnknownAnchor,
            NodeBudgetExceeded,
        }

        private sealed class MappingScope
        {
            public bool ExpectingKey = true;
            public readonly Dictionary<KeyNode, KeyOccurrence> SeenKeys = new Dictionary<KeyNode, KeyOccurrence>();
        }

        private readonly struct KeyOccurrence
        {
            public readonly Mark Start;
            public readonly Mark End;

            public KeyOccurrence(Mark start, Mark end)
            {
                Start = start;
                End = end;
            }
        }

        private sealed class ContainerFrame
        {
            public MappingScope? Mapping { get; }

            public ContainerFrame(MappingScope? mapping)
            {
                Mapping = mapping;
            }
        }

        private sealed class NodeContext
        {
            public MappingScope? KeyOf { get; set; }
            public MappingScope? ValueOf { get; set; }
        }

        private sealed class CaptureState
        {
            public int NodeCount;
            public UnsupportedReason? Unsupported;
        }

        private abstract class NodeBuilder
        {
            protected NodeBuilder(AnchorName anchor, Mark start, CaptureState capture)
            {
                Anchor = anchor;
                Start = start;
                Capture = capture;
            }

            public AnchorName Anchor { get; }
            public Mark Start { get; }
            public CaptureState Capture { get; }
            public abstract ContainerKind Kind { get; }
            public abstract void AddChild(KeyNode? child, UnsupportedReason? unsupported);
            public abstract KeyNode? Build();
        }

        private sealed class SequenceKeyBuilder : NodeBuilder
        {
            private readonly List<KeyNode> items = new List<KeyNode>();

            public SequenceKeyBuilder(AnchorName anchor, Mark start, CaptureState capture)
                : base(anchor, start, capture)
            {
            }

            public override ContainerKind Kind => ContainerKind.Sequence;

            public override void AddChild(KeyNode? child, UnsupportedReason? unsupported)
            {
                if (unsupported is { } reason)
                {
                    Capture.Unsupported ??= reason;
                }
                else
                {
                    items.Add(child!);
                }
            }

            public override KeyNode Build()
            {
                return new SequenceKeyNode(items);
            }
        }

        private sealed class MappingKeyBuilder : NodeBuilder
        {
            public MappingKeyBuilder(AnchorName anchor, Mark start, CaptureState capture)
                : base(anchor, start, capture)
            {
            }

            public override ContainerKind Kind => ContainerKind.Mapping;

            public override void AddChild(KeyNode? child, UnsupportedReason? unsupported)
            {
                if (unsupported is { } reason)
                {
                    Capture.Unsupported ??= reason;
                }
            }

            public override KeyNode? Build()
            {
                return null;
            }
        }

        private abstract class CapturedAnchor
        {
        }

        private sealed class InProgressAnchor : CapturedAnchor
        {
            public static readonly InProgressAnchor Instance = new InProgressAnchor();

            private InProgressAnchor()
            {
            }
        }

        private sealed class ResolvedAnchor : CapturedAnchor
        {
            public KeyNode Node { get; }

            public ResolvedAnchor(KeyNode node)
            {
                Node = node;
            }
        }

        private sealed class UnsupportedAnchor : CapturedAnchor
        {
            public UnsupportedReason Reason { get; }

            public UnsupportedAnchor(UnsupportedReason reason)
            {
                Reason = reason;
            }
        }

        private abstract class KeyNode : IEquatable<KeyNode>
        {
            public abstract bool Equals(KeyNode? other);

            public override bool Equals(object? obj)
            {
                return Equals(obj as KeyNode);
            }

            public abstract override int GetHashCode();
        }

        private sealed class ScalarKeyNode : KeyNode
        {
            private readonly NormalizedScalarKey key;

            public ScalarKeyNode(NormalizedScalarKey key)
            {
                this.key = key;
            }

            public override bool Equals(KeyNode? other)
            {
                return other is ScalarKeyNode scalar && key.Equals(scalar.key);
            }

            public override int GetHashCode()
            {
                return key.GetHashCode();
            }
        }

        private sealed class SequenceKeyNode : KeyNode
        {
            private readonly List<KeyNode> items;

            public SequenceKeyNode(List<KeyNode> items)
            {
                this.items = items;
            }

            public override bool Equals(KeyNode? other)
            {
                if (other is not SequenceKeyNode sequence || sequence.items.Count != items.Count)
                {
                    return false;
                }

                for (var i = 0; i < items.Count; i++)
                {
                    if (!items[i].Equals(sequence.items[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode()
            {
                var hashCode = 0;
                foreach (var item in items)
                {
                    hashCode = HashCode.CombineHashCodes(hashCode, item.GetHashCode());
                }

                return hashCode;
            }
        }
    }
}
