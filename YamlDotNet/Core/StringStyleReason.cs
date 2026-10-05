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
    /// Describes why a particular <see cref="ScalarStyle"/> was selected for a string value.
    /// </summary>
    public enum StringStyleReason
    {
        /// <summary>
        /// Plain style is safe: the value contains no indicators that forbid plain style
        /// and would not be implicitly resolved to a non-string type under the target schema.
        /// </summary>
        PlainSafe,

        /// <summary>
        /// The value is empty; a plain empty scalar would resolve to null.
        /// </summary>
        Empty,

        /// <summary>
        /// The value is a reserved word (null, boolean, number, etc.) under the target schema;
        /// plain style would be implicitly resolved to a non-string type.
        /// </summary>
        ReservedWord,

        /// <summary>
        /// The value contains a YAML indicator character, a non-printable character, or
        /// leading/trailing whitespace that forbids plain (and possibly single-quoted) style.
        /// </summary>
        Indicator,

        /// <summary>
        /// The value contains a line break; block (literal) style preserves it verbatim.
        /// </summary>
        Multiline,

        /// <summary>
        /// The value contains a line break but block style is not allowed (for example because
        /// of trailing spaces or non-printable characters); double-quoted style is required.
        /// </summary>
        MultilineBlockNotAllowed,
    }
}
