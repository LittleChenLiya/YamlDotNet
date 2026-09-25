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

namespace YamlDotNet.Serialization.Preflight
{
    /// <summary>
    /// Configures the optional deserialization preflight validation phase.
    /// When enabled, the whole event stream is buffered and validated against the
    /// target type and the registered tag mappings before any object is constructed.
    /// </summary>
    public sealed class PreflightValidationOptions
    {
        /// <summary>
        /// The default maximum number of parsing events that may be buffered during preflight validation.
        /// </summary>
        public const int DefaultMaxBufferedEvents = 100_000;

        /// <summary>
        /// The default maximum nesting depth allowed during preflight validation.
        /// </summary>
        public const int DefaultMaxDepth = 512;

        /// <summary>
        /// Gets or sets the maximum number of parsing events that may be buffered while
        /// validating the input. When the limit is exceeded, deserialization is rejected
        /// with a <see cref="PreflightValidationException"/> before any object is constructed.
        /// Must be a positive integer. Defaults to <see cref="DefaultMaxBufferedEvents"/>.
        /// </summary>
        public int MaxBufferedEvents { get; set; } = DefaultMaxBufferedEvents;

        /// <summary>
        /// Gets or sets the maximum nesting depth allowed while validating the input.
        /// Must be a positive integer. Defaults to <see cref="DefaultMaxDepth"/>.
        /// </summary>
        public int MaxDepth { get; set; } = DefaultMaxDepth;
    }
}
