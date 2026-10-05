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
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// Explicitly selects the YAML scalar style (plain, quoted or literal) that
    /// guarantees that a string re-parses to the exact same value, taking into
    /// account the implicit typing rules of a target <see cref="StringScalarSchema"/>.
    /// <para>
    /// Strings that look like another type (for example "yes", "null" or "012")
    /// are quoted so that they are not implicitly resolved as booleans, nulls or
    /// numbers. Multiline strings use the literal block style, relying on the
    /// emitter to pick the chomping indicator (strip/clip/keep) that preserves
    /// trailing line breaks and on the indentation indicator to preserve a leading
    /// indented first line. Because block scalars normalize line breaks when they
    /// are parsed, emitting with CRLF newlines still yields the original LF line
    /// breaks inside the scalar when the document is parsed again.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The rules are independent from the <see cref="Emitter"/> and can be called
    /// directly. To make the emitter apply them automatically, register
    /// <see cref="Select"/> through <see cref="EmitterSettings.WithStringStyleSelector"/>.
    /// </remarks>
    public static class StringStyleSelector
    {
        private static readonly Regex NullRegex = new Regex(@"^(~|null|Null|NULL)$", RegexOptions.CultureInvariant);
        private static readonly Regex CoreBooleanRegex = new Regex(@"^(true|True|TRUE|false|False|FALSE)$", RegexOptions.CultureInvariant);
        private static readonly Regex Yaml11BooleanRegex = new Regex(@"^(y|yes|n|no|true|false|on|off)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex DecimalIntegerRegex = new Regex(@"^[-+]?[0-9][0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex HexIntegerRegex = new Regex(@"^0x[0-9a-fA-F_]+$", RegexOptions.CultureInvariant);
        private static readonly Regex OctalIntegerRegex = new Regex(@"^0o[0-7_]+$", RegexOptions.CultureInvariant);
        private static readonly Regex BinaryIntegerRegex = new Regex(@"^0b[0-1_]+$", RegexOptions.CultureInvariant);
        private static readonly Regex SexagesimalIntegerRegex = new Regex(@"^[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+$", RegexOptions.CultureInvariant);
        private static readonly Regex FloatRegex = new Regex(@"^[-+]?(\.[0-9]+|[0-9][0-9_]*(\.[0-9_]*)?)([eE][-+]?[0-9]+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex SexagesimalFloatRegex = new Regex(@"^[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+\.[0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex InfinityRegex = new Regex(@"^[-+]?\.(inf|Inf|INF)$", RegexOptions.CultureInvariant);
        private static readonly Regex NotANumberRegex = new Regex(@"^\.(nan|NaN|NAN)$", RegexOptions.CultureInvariant);
        private static readonly Regex TimestampRegex = new Regex(@"^[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}([Tt \t].*)?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Selects the scalar style that makes <paramref name="value"/> round-trip
        /// unchanged through emission and parsing, and explains the choice.
        /// </summary>
        /// <param name="value">The string to emit.</param>
        /// <param name="schema">
        /// The schema whose implicit resolution rules the emitted document must be
        /// robust against. Defaults to <see cref="StringScalarSchema.Yaml11"/>, which
        /// matches this library's deserializer.
        /// </param>
        public static StringStyleDecision Select(string value, StringScalarSchema schema = StringScalarSchema.Yaml11)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                return new StringStyleDecision(ScalarStyle.SingleQuoted,
                    "An empty plain scalar would be implicitly resolved as null; single quotes preserve the empty string.");
            }

            if (ContainsNonPrintableCharacters(value))
            {
                return new StringStyleDecision(ScalarStyle.DoubleQuoted,
                    "The value contains characters that are not printable in YAML and must be written as escape sequences.");
            }

            if (value.Contains('\n') || value.Contains('\r'))
            {
                return SelectMultilineStyle(value);
            }

            if (ResolvesToNonString(value, schema, out var resolvedAs))
            {
                return new StringStyleDecision(ScalarStyle.SingleQuoted,
                    $"A plain scalar '{value}' would be implicitly resolved as {resolvedAs} by the {schema} schema; single quotes keep it a string.");
            }

            if (!IsPlainSafe(value, out var plainProblem))
            {
                return new StringStyleDecision(ScalarStyle.SingleQuoted,
                    plainProblem + " Single quotes preserve the value verbatim.");
            }

            return new StringStyleDecision(ScalarStyle.Plain,
                "The value contains no indicators, is not implicitly resolved as another type and round-trips as a plain scalar.");
        }

        /// <summary>
        /// Creates a scalar event for <paramref name="value"/> with the style chosen
        /// by <see cref="Select"/>, ready to be sent to an <see cref="IEmitter"/>.
        /// </summary>
        public static Scalar CreateScalar(string value, StringScalarSchema schema, out StringStyleDecision decision)
        {
            decision = Select(value, schema);
            return new Scalar(AnchorName.Empty, TagName.Empty, value, decision.Style, true, true);
        }

        /// <summary>
        /// Creates a scalar event for <paramref name="value"/> with the style chosen
        /// by <see cref="Select"/>, ready to be sent to an <see cref="IEmitter"/>.
        /// </summary>
        public static Scalar CreateScalar(string value, StringScalarSchema schema = StringScalarSchema.Yaml11)
        {
            return CreateScalar(value, schema, out _);
        }

        private static StringStyleDecision SelectMultilineStyle(string value)
        {
            if (value.Contains('\r'))
            {
                return new StringStyleDecision(ScalarStyle.DoubleQuoted,
                    "The value contains carriage returns, which block scalars would normalize to line feeds; escape sequences preserve them exactly.");
            }

            if (value[0] == '\n')
            {
                return new StringStyleDecision(ScalarStyle.DoubleQuoted,
                    "The value starts with a line break, which a block scalar cannot represent unambiguously; escape sequences preserve it exactly.");
            }

            if (EndsLineWithSpace(value))
            {
                return new StringStyleDecision(ScalarStyle.DoubleQuoted,
                    "A line of the value ends with a space, which a block scalar cannot represent reliably; escape sequences preserve the trailing spaces.");
            }

            return new StringStyleDecision(ScalarStyle.Literal,
                "The value spans multiple lines; the literal block style preserves every line, "
                + DescribeChomping(value)
                + (value[0] == ' ' ? ", and the indentation indicator preserves the indented first line." : "."));
        }

        private static string DescribeChomping(string value)
        {
            if (value[value.Length - 1] != '\n')
            {
                return "strip chomping ('|-') preserves the absence of a final line break";
            }

            if (value.Length >= 2 && value[value.Length - 2] == '\n')
            {
                return "keep chomping ('|+') preserves all the trailing line breaks";
            }

            return "clip chomping ('|') preserves the single trailing line break";
        }

        private static bool EndsLineWithSpace(string value)
        {
            for (var i = 0; i < value.Length; ++i)
            {
                if (value[i] == ' ' && (i + 1 == value.Length || value[i + 1] == '\n'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsNonPrintableCharacters(string value)
        {
            foreach (var character in value)
            {
                if (!IsPrintable(character) || character == '\t')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPrintable(char character)
        {
            // Mirrors the YAML printable character set used by the scanner.
            return
                character == '\x9' ||
                character == '\xA' ||
                character == '\xD' ||
                (character >= '\x20' && character <= '\x7E') ||
                character == '\x85' ||
                (character >= '\xA0' && character <= '\xD7FF') ||
                (character >= '\xE000' && character <= '\xFFFD');
        }

        private static bool ResolvesToNonString(string value, StringScalarSchema schema, out string resolvedAs)
        {
            if (NullRegex.IsMatch(value))
            {
                resolvedAs = "null";
                return true;
            }

            if (schema == StringScalarSchema.Failsafe)
            {
                resolvedAs = string.Empty;
                return false;
            }

            if (CoreBooleanRegex.IsMatch(value) ||
                (schema == StringScalarSchema.Yaml11 && Yaml11BooleanRegex.IsMatch(value)))
            {
                resolvedAs = "a boolean";
                return true;
            }

            if (DecimalIntegerRegex.IsMatch(value) ||
                HexIntegerRegex.IsMatch(value) ||
                OctalIntegerRegex.IsMatch(value) ||
                (schema == StringScalarSchema.Yaml11 &&
                    (BinaryIntegerRegex.IsMatch(value) || SexagesimalIntegerRegex.IsMatch(value))))
            {
                resolvedAs = "an integer";
                return true;
            }

            if (FloatRegex.IsMatch(value) ||
                InfinityRegex.IsMatch(value) ||
                NotANumberRegex.IsMatch(value) ||
                (schema == StringScalarSchema.Yaml11 && SexagesimalFloatRegex.IsMatch(value)))
            {
                resolvedAs = "a floating point number";
                return true;
            }

            if (schema == StringScalarSchema.Yaml11 && TimestampRegex.IsMatch(value))
            {
                resolvedAs = "a timestamp";
                return true;
            }

            resolvedAs = string.Empty;
            return false;
        }

        private static bool IsPlainSafe(string value, out string problem)
        {
            var first = value[0];

            if (first == ' ' || value[value.Length - 1] == ' ')
            {
                problem = "The value has leading or trailing spaces, which a plain scalar would lose.";
                return false;
            }

            if (first == '#' || first == '&' || first == '*' || first == '!' || first == '|' || first == '>' ||
                first == '%' || first == '@' || first == '`' || first == '\'' || first == '"' ||
                first == ',' || first == '[' || first == ']' || first == '{' || first == '}')
            {
                problem = $"The value starts with the indicator character '{first}'.";
                return false;
            }

            if ((first == '-' || first == '?' || first == ':') &&
                (value.Length == 1 || value[1] == ' '))
            {
                problem = $"The value starts with the indicator character '{first}' followed by a space.";
                return false;
            }

            if (value.StartsWith("---", StringComparison.Ordinal) || value.StartsWith("...", StringComparison.Ordinal))
            {
                problem = "The value starts with a document boundary marker.";
                return false;
            }

            for (var i = 0; i < value.Length; ++i)
            {
                var character = value[i];
                if (character == ',' || character == '[' || character == ']' || character == '{' || character == '}')
                {
                    problem = $"The value contains the flow indicator character '{character}', which is not safe in every context.";
                    return false;
                }

                if (character == ':' && (i + 1 == value.Length || value[i + 1] == ' '))
                {
                    problem = "The value contains ':' followed by a space, which would start a mapping.";
                    return false;
                }

                if (character == '#' && i > 0 && value[i - 1] == ' ')
                {
                    problem = "The value contains ' #', which would start a comment.";
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }
    }
}
