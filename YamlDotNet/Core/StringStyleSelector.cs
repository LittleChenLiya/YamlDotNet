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
using System.Text.RegularExpressions;
using YamlDotNet.Serialization.Schemas;

namespace YamlDotNet.Core
{
    /// <summary>
    /// Selects a safe <see cref="ScalarStyle"/> for a string value based on the target schema,
    /// ensuring that the emitted scalar round-trips to the original string when re-parsed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The selector is stateless and can be called independently of the <see cref="Emitter"/>.
    /// Given a string value and the resolved tag of the scalar, it returns the recommended
    /// style together with the reason why that style was chosen.
    /// </para>
    /// <para>
    /// Plain style is only selected when the value would not be implicitly resolved to a
    /// non-string type under the target schema (for example <c>yes</c>, <c>null</c> or a
    /// numeric-looking string) and when the value contains no indicator characters that forbid
    /// plain style. Multiline values use literal block style (with the appropriate chomping
    /// indicator) so that line breaks are preserved verbatim.
    /// </para>
    /// </remarks>
    public static class StringStyleSelector
    {
        // This pattern matches strings that are special both in YAML 1.1 and 1.2.
        // It mirrors the implicit resolution rules used by the deserializer so that a plain
        // scalar would not be resolved to a non-string type.
        private static readonly Regex ReservedWordPattern = new Regex(
            @"^("
                + @"null|Null|NULL|\~"
                + @"|true|True|TRUE|false|False|FALSE"
                + @"|y|Y|yes|Yes|YES|n|N|no|No|NO"
                + @"|on|On|ON|off|Off|OFF"
                + @"|[-+]?0b[0-1_]+" // int base 2
                + @"|[-+]?0o?[0-7_]+" // int base 8 both with and without "o"
                + @"|[-+]?(0|[1-9][0-9_]*)" // int base 10
                + @"|[-+]?0x[0-9a-fA-F_]+" // int base 16
                + @"|[-+]?[1-9][0-9_]*(:[0-5]?[0-9])+" // int base 60
                + @"|[-+]?([0-9][0-9_]*)?\.[0-9_]*([eE][-+][0-9]+)?" // float base 10
                + @"|[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+\.[0-9_]*" // float base 60
                + @"|[-+]?\.(inf|Inf|INF)"
                + @"|\.(nan|NaN|NAN)"
            + @")$",
            StandardRegexOptions.Compiled);

        /// <summary>
        /// Selects a safe scalar style for the specified string value.
        /// </summary>
        /// <param name="value">The string value to emit.</param>
        /// <param name="tag">The resolved tag of the scalar (use <see cref="TagName.Empty"/> for an implicit scalar).</param>
        /// <returns>The selected style together with the reason.</returns>
        public static ScalarStyleSelection Select(string value, TagName tag)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            // An explicit non-string, non-null tag means the value is being emitted as that
            // type; plain style is appropriate and the caller is responsible for the value.
            if (!tag.IsEmpty && tag != FailsafeSchema.Tags.Str && tag != JsonSchema.Tags.Null)
            {
                return new ScalarStyleSelection(ScalarStyle.Plain, StringStyleReason.PlainSafe);
            }

            // Empty value: a plain empty scalar resolves to null.
            if (value.Length == 0)
            {
                if (tag == JsonSchema.Tags.Null)
                {
                    return new ScalarStyleSelection(ScalarStyle.Plain, StringStyleReason.PlainSafe);
                }

                return new ScalarStyleSelection(ScalarStyle.SingleQuoted, StringStyleReason.Empty);
            }

            var analysis = Analyze(value);

            // Multiline values: prefer literal block style so that line breaks are preserved
            // verbatim. Fall back to double-quoted when block style is not allowed.
            if (analysis.IsMultiline)
            {
                // Block style normalizes carriage returns to line breaks during parsing, so a
                // value containing '\r' cannot round-trip through literal style. Use double-quoted
                // style, which preserves '\r' via escape sequences.
                if (analysis.IsBlockAllowed && !analysis.HasCarriageReturn)
                {
                    return new ScalarStyleSelection(ScalarStyle.Literal, StringStyleReason.Multiline);
                }

                return new ScalarStyleSelection(ScalarStyle.DoubleQuoted, StringStyleReason.MultilineBlockNotAllowed);
            }

            // Single-line values: plain is safe only when the value contains no indicators and
            // would not be implicitly resolved to a non-string type.
            var isPlainAllowed = analysis.IsBlockPlainAllowed;

            if (tag == FailsafeSchema.Tags.Str)
            {
                // An explicit string tag prevents implicit resolution, so plain is safe as
                // long as the value contains no indicator characters.
                if (isPlainAllowed)
                {
                    return new ScalarStyleSelection(ScalarStyle.Plain, StringStyleReason.PlainSafe);
                }

                return Quote(analysis, StringStyleReason.Indicator);
            }

            // Implicit (or null) tag: check whether the value would be implicitly resolved.
            if (IsReservedWord(value))
            {
                return Quote(analysis, StringStyleReason.ReservedWord);
            }

            if (isPlainAllowed)
            {
                return new ScalarStyleSelection(ScalarStyle.Plain, StringStyleReason.PlainSafe);
            }

            return Quote(analysis, StringStyleReason.Indicator);
        }

        /// <summary>
        /// Determines whether the specified value is a reserved word that would be implicitly
        /// resolved to a non-string type (null, boolean, number, etc.) under the target schema.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns><see langword="true"/> if the value is a reserved word; otherwise <see langword="false"/>.</returns>
        public static bool IsReservedWord(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return ReservedWordPattern.IsMatch(value);
        }

        /// <summary>
        /// Determines whether the specified value contains a line break.
        /// </summary>
        public static bool HasLineBreak(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            foreach (var c in value)
            {
                if (IsBreak(c))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether plain style is allowed for the specified value.
        /// </summary>
        public static bool IsPlainScalarAllowed(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Analyze(value).IsBlockPlainAllowed;
        }

        /// <summary>
        /// Determines whether block (literal) style is allowed for the specified value.
        /// </summary>
        public static bool IsBlockScalarAllowed(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Analyze(value).IsBlockAllowed;
        }

        private static ScalarStyleSelection Quote(in ValueAnalysis analysis, StringStyleReason reason)
        {
            if (analysis.IsSingleQuotedAllowed && !analysis.HasSingleQuotes)
            {
                return new ScalarStyleSelection(ScalarStyle.SingleQuoted, reason);
            }

            return new ScalarStyleSelection(ScalarStyle.DoubleQuoted, reason);
        }

        private static ValueAnalysis Analyze(string value)
        {
            var flowIndicators = false;
            var blockIndicators = false;

            if (value.StartsWith("---", StringComparison.Ordinal) || value.StartsWith("...", StringComparison.Ordinal))
            {
                flowIndicators = true;
                blockIndicators = true;
            }

            var leadingSpace = false;
            var leadingBreak = false;
            var trailingSpace = false;
            var trailingBreak = false;
            var leadingQuote = false;

            var breakSpace = false;
            var spaceBreak = false;
            var previousSpace = false;
            var previousBreak = false;
            var lineOfSpaces = false;

            var lineBreaks = false;
            var hasCarriageReturn = false;
            var specialCharacters = false;
            var singleQuotes = false;
            var linesOfSpaces = false;

            var isFirst = true;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var followedByWhitespace = index + 1 >= value.Length || IsWhiteBreakOrZero(value[index + 1]);

                if (isFirst)
                {
                    if (IsFlowOrBlockIndicator(character))
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                        leadingQuote = character == '\'';
                        singleQuotes |= character == '\'';
                    }

                    if (character == '?' || character == ':')
                    {
                        flowIndicators = true;
                        if (followedByWhitespace)
                        {
                            blockIndicators = true;
                        }
                    }

                    if (character == '-' && followedByWhitespace)
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                    }
                }
                else
                {
                    if (IsFlowIndicator(character))
                    {
                        flowIndicators = true;
                    }

                    if (character == ':')
                    {
                        flowIndicators = true;
                        if (followedByWhitespace)
                        {
                            blockIndicators = true;
                        }
                    }

                    if (character == '#' && previousSpace)
                    {
                        flowIndicators = true;
                        blockIndicators = true;
                    }

                    singleQuotes |= character == '\'';
                }

                if (!specialCharacters && !IsPrintable(character))
                {
                    specialCharacters = true;
                }

                if (IsBreak(character))
                {
                    lineBreaks = true;
                    if (character == '\r')
                    {
                        hasCarriageReturn = true;
                    }
                }

                if (IsWhite(character))
                {
                    if (isFirst)
                    {
                        leadingSpace = true;
                    }

                    if (index >= value.Length - 1)
                    {
                        trailingSpace = true;
                    }

                    if (previousBreak)
                    {
                        breakSpace = true;
                        lineOfSpaces = true;
                    }

                    previousSpace = true;
                    previousBreak = false;
                }
                else if (IsBreak(character))
                {
                    if (isFirst)
                    {
                        leadingBreak = true;
                    }

                    if (index >= value.Length - 1)
                    {
                        trailingBreak = true;
                    }

                    if (previousSpace)
                    {
                        spaceBreak = true;
                    }

                    if (lineOfSpaces)
                    {
                        linesOfSpaces = true;
                    }

                    previousSpace = false;
                    previousBreak = true;
                }
                else
                {
                    previousSpace = false;
                    previousBreak = false;
                    lineOfSpaces = false;
                }

                isFirst = false;
            }

            var isFlowPlainAllowed = true;
            var isBlockPlainAllowed = true;
            var isSingleQuotedAllowed = true;
            var isBlockAllowed = true;

            if (leadingSpace || leadingBreak || trailingSpace || trailingBreak || leadingQuote)
            {
                isFlowPlainAllowed = false;
                isBlockPlainAllowed = false;
            }

            if (trailingSpace)
            {
                isBlockAllowed = false;
            }

            if (breakSpace)
            {
                isFlowPlainAllowed = false;
                isBlockPlainAllowed = false;
                isSingleQuotedAllowed = false;
            }

            if (spaceBreak || specialCharacters)
            {
                isFlowPlainAllowed = false;
                isBlockPlainAllowed = false;
                isSingleQuotedAllowed = false;
            }

            if (linesOfSpaces)
            {
                isBlockAllowed = false;
            }

            if (lineBreaks)
            {
                isFlowPlainAllowed = false;
                isBlockPlainAllowed = false;
            }

            if (flowIndicators)
            {
                isFlowPlainAllowed = false;
            }

            if (blockIndicators)
            {
                isBlockPlainAllowed = false;
            }

            return new ValueAnalysis(
                isMultiline: lineBreaks,
                isFlowPlainAllowed: isFlowPlainAllowed,
                isBlockPlainAllowed: isBlockPlainAllowed,
                isSingleQuotedAllowed: isSingleQuotedAllowed,
                isBlockAllowed: isBlockAllowed,
                hasSingleQuotes: singleQuotes,
                hasCarriageReturn: hasCarriageReturn);
        }

        private static bool IsWhite(char character) => character == ' ' || character == '\t';

        private static bool IsFlowOrBlockIndicator(char character) =>
            character == '#' || character == ',' || character == '[' || character == ']' ||
            character == '{' || character == '}' || character == '&' || character == '*' ||
            character == '!' || character == '|' || character == '>' || character == '"' ||
            character == '%' || character == '@' || character == '`' || character == '\'';

        private static bool IsFlowIndicator(char character) =>
            character == ',' || character == '?' || character == '[' || character == ']' ||
            character == '{' || character == '}';

        private static bool IsBreak(char character) =>
            character == '\r' || character == '\n' || character == '\x85' ||
            character == '\x2028' || character == '\x2029';

        private static bool IsWhiteBreakOrZero(char character) =>
            character == '\0' || IsWhite(character) || IsBreak(character);

        private static bool IsPrintable(char character) =>
            character == '\x9' ||
            character == '\xA' ||
            character == '\xD' ||
            (character >= '\x20' && character <= '\x7E') ||
            character == '\x85' ||
            (character >= '\xA0' && character <= '\xD7FF') ||
            (character >= '\xE000' && character <= '\xFFFD');

        private readonly struct ValueAnalysis
        {
            public readonly bool IsMultiline;
            public readonly bool IsFlowPlainAllowed;
            public readonly bool IsBlockPlainAllowed;
            public readonly bool IsSingleQuotedAllowed;
            public readonly bool IsBlockAllowed;
            public readonly bool HasSingleQuotes;
            public readonly bool HasCarriageReturn;

            public ValueAnalysis(
                bool isMultiline,
                bool isFlowPlainAllowed,
                bool isBlockPlainAllowed,
                bool isSingleQuotedAllowed,
                bool isBlockAllowed,
                bool hasSingleQuotes,
                bool hasCarriageReturn)
            {
                IsMultiline = isMultiline;
                IsFlowPlainAllowed = isFlowPlainAllowed;
                IsBlockPlainAllowed = isBlockPlainAllowed;
                IsSingleQuotedAllowed = isSingleQuotedAllowed;
                IsBlockAllowed = isBlockAllowed;
                HasSingleQuotes = hasSingleQuotes;
                HasCarriageReturn = hasCarriageReturn;
            }
        }
    }
}
