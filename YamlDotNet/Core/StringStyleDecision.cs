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

namespace YamlDotNet.Core
{
    /// <summary>
    /// The result of <see cref="StringStyleSelector.Select"/>: the scalar style that
    /// should be used to emit a string, together with a human-readable explanation
    /// of why that style is required for the value to round-trip unchanged.
    /// </summary>
    public readonly struct StringStyleDecision : IEquatable<StringStyleDecision>
    {
        /// <summary>
        /// The scalar style to use when emitting the string.
        /// </summary>
        public ScalarStyle Style { get; }

        /// <summary>
        /// Explains why <see cref="Style"/> was selected for the value.
        /// </summary>
        public string Reason { get; }

        public StringStyleDecision(ScalarStyle style, string reason)
        {
            Style = style;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public bool Equals(StringStyleDecision other) => Style == other.Style && Reason == other.Reason;

        public override bool Equals(object? obj) => obj is StringStyleDecision other && Equals(other);

        public override int GetHashCode() => HashCode.CombineHashCodes((int)Style, Reason);

        public static bool operator ==(StringStyleDecision left, StringStyleDecision right) => left.Equals(right);

        public static bool operator !=(StringStyleDecision left, StringStyleDecision right) => !left.Equals(right);

        public override string ToString() => $"{Style} ({Reason})";
    }
}
