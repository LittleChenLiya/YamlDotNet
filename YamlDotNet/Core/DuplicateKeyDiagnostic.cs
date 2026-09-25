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
    /// Describes the kind of a diagnostic reported by <see cref="DuplicateKeyDetectorParser"/>.
    /// </summary>
    public enum DuplicateKeyDiagnosticKind
    {
        /// <summary>
        /// Two keys in the same mapping compare as equal. The locations of both the first
        /// occurrence and the duplicate are available on the diagnostic.
        /// </summary>
        DuplicateKey,

        /// <summary>
        /// A key cannot be compared for equality, for example because it contains an alias
        /// cycle, references an unknown anchor, or canonicalization was stopped after the
        /// node budget was exceeded. The key is reported instead of being silently ignored.
        /// </summary>
        UnsupportedKey,

        /// <summary>
        /// The configured node budget was exceeded. Duplicate detection is disabled for the
        /// remainder of the current document.
        /// </summary>
        NodeBudgetExceeded,
    }

    /// <summary>
    /// Represents a diagnostic reported by <see cref="DuplicateKeyDetectorParser"/> while
    /// checking a YAML event stream for duplicate mapping keys.
    /// </summary>
    public sealed class DuplicateKeyDiagnostic
    {
        /// <summary>
        /// Gets the kind of this diagnostic.
        /// </summary>
        public DuplicateKeyDiagnosticKind Kind { get; }

        /// <summary>
        /// Gets the start of the first occurrence of the key.
        /// Only meaningful when <see cref="Kind"/> is <see cref="DuplicateKeyDiagnosticKind.DuplicateKey"/>;
        /// otherwise it is <see cref="Mark.Empty"/>.
        /// </summary>
        public Mark FirstKeyStart { get; }

        /// <summary>
        /// Gets the end of the first occurrence of the key.
        /// Only meaningful when <see cref="Kind"/> is <see cref="DuplicateKeyDiagnosticKind.DuplicateKey"/>;
        /// otherwise it is <see cref="Mark.Empty"/>.
        /// </summary>
        public Mark FirstKeyEnd { get; }

        /// <summary>
        /// Gets the start of the key that triggered this diagnostic.
        /// </summary>
        public Mark KeyStart { get; }

        /// <summary>
        /// Gets the end of the key that triggered this diagnostic.
        /// </summary>
        public Mark KeyEnd { get; }

        /// <summary>
        /// Gets a human-readable description of this diagnostic.
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
            this.Kind = kind;
            this.FirstKeyStart = firstKeyStart;
            this.FirstKeyEnd = firstKeyEnd;
            this.KeyStart = keyStart;
            this.KeyEnd = keyEnd;
            this.Message = message;
        }

        /// <summary>
        /// Returns a string that represents this diagnostic.
        /// </summary>
        public override string ToString()
        {
            return $"{this.Kind} at {this.KeyStart}: {this.Message}";
        }
    }
}

