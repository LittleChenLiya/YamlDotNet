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
    /// Represents the normalized form of a scalar mapping key, produced by an <see cref="IScalarKeyNormalizer"/>.
    /// Two scalar keys are considered equal when both their <see cref="Kind"/> and <see cref="Value"/> are equal
    /// (ordinal comparison).
    /// </summary>
    public readonly struct NormalizedScalarKey : IEquatable<NormalizedScalarKey>
    {
        /// <summary>
        /// Gets the kind of the normalized key, which identifies the domain of the value
        /// (for example "str", "int", "float", "bool" or "null").
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets the canonical representation of the value within its <see cref="Kind"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedScalarKey"/> struct.
        /// </summary>
        /// <param name="kind">The kind of the normalized key.</param>
        /// <param name="value">The canonical representation of the value.</param>
        public NormalizedScalarKey(string kind, string value)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc />
        public bool Equals(NormalizedScalarKey other)
        {
            return string.Equals(Kind, other.Kind, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is NormalizedScalarKey other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.CombineHashCodes(Kind.GetHashCode(), Value.GetHashCode());
        }

        /// <summary>
        /// Indicates whether two <see cref="NormalizedScalarKey"/> instances are equal.
        /// </summary>
        public static bool operator ==(NormalizedScalarKey left, NormalizedScalarKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Indicates whether two <see cref="NormalizedScalarKey"/> instances are not equal.
        /// </summary>
        public static bool operator !=(NormalizedScalarKey left, NormalizedScalarKey right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString() => $"{Kind}:{Value}";
    }
}
