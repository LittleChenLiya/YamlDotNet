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

namespace YamlDotNet.Core
{
    /// <summary>
    /// Identifies the kind of a <see cref="DuplicateKeyDiagnostic"/>.
    /// </summary>
    public enum DuplicateKeyDiagnosticKind
    {
        /// <summary>
        /// A key is equal to a key that was already seen in the same mapping.
        /// </summary>
        DuplicateKey,

        /// <summary>
        /// A key is (or contains) a mapping, which cannot be compared for equality.
        /// </summary>
        UnsupportedMappingKey,

        /// <summary>
        /// A key contains an alias that refers to the anchor that is currently being defined,
        /// forming a cycle that cannot be compared for equality.
        /// </summary>
        UnsupportedAliasCycle,

        /// <summary>
        /// A key contains an alias that refers to an anchor that was never defined.
        /// </summary>
        UnsupportedUnknownAnchor,

        /// <summary>
        /// A key contains more nodes than the configured node budget allows.
        /// </summary>
        UnsupportedNodeBudgetExceeded,
    }

    /// <summary>
    /// Describes a duplicate or unsupported mapping key detected by <see cref="DuplicateKeyCheckingParser"/>.
    /// </summary>
    public sealed class DuplicateKeyDiagnostic
    {
        /// <summary>
        /// Gets the kind of this diagnostic.
        /// </summary>
        public DuplicateKeyDiagnosticKind Kind { get; }

        /// <summary>
        /// Gets the position where the first occurrence of the key starts.
        /// Only meaningful when <see cref="Kind"/> is <see cref="DuplicateKeyDiagnosticKind.DuplicateKey"/>;
        /// otherwise it is <see cref="Mark.Empty"/>.
        /// </summary>
        public Mark FirstKeyStart { get; }

        /// <summary>
        /// Gets the position where the first occurrence of the key ends.
        /// Only meaningful when <see cref="Kind"/> is <see cref="DuplicateKeyDiagnosticKind.DuplicateKey"/>;
        /// otherwise it is <see cref="Mark.Empty"/>.
        /// </summary>
        public Mark FirstKeyEnd { get; }

        /// <summary>
        /// Gets the position where the offending (duplicate or unsupported) key starts.
        /// </summary>
        public Mark KeyStart { get; }

        /// <summary>
        /// Gets the position where the offending (duplicate or unsupported) key ends.
        /// </summary>
        public Mark KeyEnd { get; }

        /// <summary>
        /// Gets a human-readable description of the diagnostic.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateKeyDiagnostic"/> class.
        /// </summary>
        public DuplicateKeyDiagnostic(
            DuplicateKeyDiagnosticKind kind,
            Mark firstKeyStart,
            Mark firstKeyEnd,
            Mark keyStart,
            Mark keyEnd,
            string message)
        {
            Kind = kind;
            FirstKeyStart = firstKeyStart;
            FirstKeyEnd = firstKeyEnd;
            KeyStart = keyStart;
            KeyEnd = keyEnd;
            Message = message;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Kind == DuplicateKeyDiagnosticKind.DuplicateKey
                ? $"{Kind} at {KeyStart} (first occurrence at {FirstKeyStart}): {Message}"
                : $"{Kind} at {KeyStart}: {Message}";
        }
    }
}
