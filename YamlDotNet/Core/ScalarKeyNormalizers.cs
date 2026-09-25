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
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// Provides the built-in <see cref="IScalarKeyNormalizer"/> implementations that can be
    /// used with <see cref="DuplicateKeyDetectorParser"/>.
    /// </summary>
    public static class ScalarKeyNormalizers
    {
        /// <summary>
        /// Gets a normalizer that compares scalar keys by their exact string value,
        /// using ordinal comparison. No type resolution is performed, so the keys
        /// <c>1</c> and <c>01</c> are considered different.
        /// </summary>
        public static IScalarKeyNormalizer Ordinal { get; } = new StringScalarKeyNormalizer();

        /// <summary>
        /// Gets a normalizer that resolves plain scalars according to the YAML 1.2 Core Schema
        /// (null, bool, int and float) before comparing them. Numeric values are parsed using
        /// the invariant culture, so the keys <c>1</c> and <c>01</c> are considered equal,
        /// while a scalar with an explicit <c>!!str</c> tag or a non-plain style is always
        /// treated as a string.
        /// </summary>
        public static IScalarKeyNormalizer CoreSchema { get; } = new CoreSchemaScalarKeyNormalizer();

        private sealed class StringScalarKeyNormalizer : IScalarKeyNormalizer
        {
            public string NormalizeKey(Scalar scalar)
            {
                return "str:" + scalar.Value;
            }
        }

        private sealed class CoreSchemaScalarKeyNormalizer : IScalarKeyNormalizer
        {
            private const string CoreSchemaTagPrefix = "tag:yaml.org,2002:";

            private static readonly Regex IntegerPattern = new Regex(@"^[-+]?[0-9]+$", StandardRegexOptions.Compiled);
            private static readonly Regex OctalPattern = new Regex(@"^0o[0-7]+$", StandardRegexOptions.Compiled);
            private static readonly Regex HexadecimalPattern = new Regex(@"^0x[0-9a-fA-F]+$", StandardRegexOptions.Compiled);
            private static readonly Regex FloatPattern = new Regex(@"^[-+]?(.[0-9]+|[0-9]+(.[0-9]*)?)([eE][-+]?[0-9]+)?$", StandardRegexOptions.Compiled);

            public string NormalizeKey(Scalar scalar)
            {
                if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
                {
                    return NormalizeTagged(scalar);
                }

                if (scalar.Style != ScalarStyle.Plain)
                {
                    // Only plain scalars are resolved by the core schema.
                    return StringForm(scalar.Value);
                }

                return ResolvePlain(scalar.Value);
            }

            private static string NormalizeTagged(Scalar scalar)
            {
                var tag = scalar.Tag.Value;
                if (tag.StartsWith(CoreSchemaTagPrefix, StringComparison.Ordinal))
                {
                    var suffix = tag.Substring(CoreSchemaTagPrefix.Length);
                    switch (suffix)
                    {
                        case "str":
                            return StringForm(scalar.Value);
                        case "null":
                            return "null:";
                        case "bool":
                            return scalar.Value == "true" ? "bool:true" : "bool:false";
                        case "int":
                            return TryNormalizeInteger(scalar.Value) ?? StringForm(scalar.Value);
                        case "float":
                            return TryNormalizeFloat(scalar.Value) ?? StringForm(scalar.Value);
                    }
                }

                // Unknown explicit tags keep their identity so that, for example,
                // "!foo 1" and "1" never compare as equal.
                return "tag:" + tag + ":" + scalar.Value;
            }

            private static string ResolvePlain(string value)
            {
                switch (value)
                {
                    case "":
                    case "~":
                    case "null":
                    case "Null":
                    case "NULL":
                        return "null:";
                    case "true":
                    case "True":
                    case "TRUE":
                        return "bool:true";
                    case "false":
                    case "False":
                    case "FALSE":
                        return "bool:false";
                    case ".inf":
                    case ".Inf":
                    case ".INF":
                    case "+.inf":
                    case "+.Inf":
                    case "+.INF":
                        return "float:+inf";
                    case "-.inf":
                    case "-.Inf":
                    case "-.INF":
                        return "float:-inf";
                    case ".nan":
                    case ".NaN":
                    case ".NAN":
                        return "float:nan";
                }

                return TryNormalizeInteger(value)
                    ?? TryNormalizeFloat(value)
                    ?? StringForm(value);
            }

            private static string? TryNormalizeInteger(string value)
            {
                if (IntegerPattern.IsMatch(value))
                {
                    // BigInteger parses decimal digits independently of the current culture.
                    var number = BigInteger.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return "int:" + number.ToString(CultureInfo.InvariantCulture);
                }

                if (OctalPattern.IsMatch(value))
                {
                    return "int:" + ParseUnsigned(value.Substring(2), 8).ToString(CultureInfo.InvariantCulture);
                }

                if (HexadecimalPattern.IsMatch(value))
                {
                    return "int:" + ParseUnsigned(value.Substring(2), 16).ToString(CultureInfo.InvariantCulture);
                }

                return null;
            }

            private static string? TryNormalizeFloat(string value)
            {
                if (!FloatPattern.IsMatch(value))
                {
                    return null;
                }

                var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                return "float:" + number.ToString("R", CultureInfo.InvariantCulture);
            }

            private static BigInteger ParseUnsigned(string digits, int numberBase)
            {
                var result = BigInteger.Zero;
                foreach (var c in digits)
                {
                    var digit = c <= '9' ? c - '0' : (char.ToUpperInvariant(c) - 'A') + 10;
                    result = (result * numberBase) + digit;
                }

                return result;
            }

            private static string StringForm(string value)
            {
                return "str:" + value;
            }
        }
    }
}
