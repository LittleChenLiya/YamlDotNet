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
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// Provides built-in <see cref="IScalarKeyNormalizer"/> implementations.
    /// </summary>
    public static class ScalarKeyNormalizers
    {
        /// <summary>
        /// Gets a normalizer that compares scalar keys by their raw string value, using ordinal comparison.
        /// Tags and styles are ignored, so <c>1</c> and <c>01</c> are considered different keys.
        /// </summary>
        public static IScalarKeyNormalizer Ordinal { get; } = new OrdinalScalarKeyNormalizer();

        /// <summary>
        /// Gets a normalizer that resolves plain scalars according to the YAML 1.2 Core Schema
        /// (null, bool, int and float) before comparing them. Explicit <c>tag:yaml.org,2002:*</c> tags
        /// are honored. All number parsing is culture-invariant, so <c>1</c> and <c>01</c> are
        /// considered the same key, while <c>!!str 1</c> and <c>1</c> are not.
        /// </summary>
        public static IScalarKeyNormalizer CoreSchema { get; } = new CoreSchemaScalarKeyNormalizer();

        private sealed class OrdinalScalarKeyNormalizer : IScalarKeyNormalizer
        {
            public NormalizedScalarKey Normalize(Scalar scalar)
            {
                return new NormalizedScalarKey("str", scalar.Value);
            }
        }

        private sealed class CoreSchemaScalarKeyNormalizer : IScalarKeyNormalizer
        {
            private const string StrTag = "tag:yaml.org,2002:str";
            private const string IntTag = "tag:yaml.org,2002:int";
            private const string FloatTag = "tag:yaml.org,2002:float";
            private const string BoolTag = "tag:yaml.org,2002:bool";
            private const string NullTag = "tag:yaml.org,2002:null";

            public NormalizedScalarKey Normalize(Scalar scalar)
            {
                if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
                {
                    return NormalizeExplicitTag(scalar);
                }

                if (scalar.Style == ScalarStyle.Plain)
                {
                    return ResolvePlain(scalar.Value);
                }

                // Non-plain scalars without an explicit tag always resolve to strings.
                return new NormalizedScalarKey("str", scalar.Value);
            }

            private static NormalizedScalarKey NormalizeExplicitTag(Scalar scalar)
            {
                var tag = scalar.Tag.Value;
                switch (tag)
                {
                    case StrTag:
                        return new NormalizedScalarKey("str", scalar.Value);
                    case NullTag:
                        return IsNull(scalar.Value)
                            ? new NormalizedScalarKey("null", string.Empty)
                            : Unresolvable(scalar);
                    case BoolTag:
                        return TryResolveBool(scalar.Value, out var boolKey)
                            ? boolKey
                            : Unresolvable(scalar);
                    case IntTag:
                        return TryResolveInt(scalar.Value, out var intKey)
                            ? intKey
                            : Unresolvable(scalar);
                    case FloatTag:
                        return TryResolveFloat(scalar.Value, out var floatKey)
                            ? floatKey
                            : Unresolvable(scalar);
                    default:
                        // Unknown explicit tags form their own equality domain.
                        return new NormalizedScalarKey("tag:" + tag, scalar.Value);
                }
            }

            private static NormalizedScalarKey Unresolvable(Scalar scalar)
            {
                return new NormalizedScalarKey("invalid:" + scalar.Tag.Value, scalar.Value);
            }

            private static NormalizedScalarKey ResolvePlain(string value)
            {
                if (IsNull(value))
                {
                    return new NormalizedScalarKey("null", string.Empty);
                }

                if (TryResolveBool(value, out var boolKey))
                {
                    return boolKey;
                }

                if (TryResolveInt(value, out var intKey))
                {
                    return intKey;
                }

                if (TryResolveFloat(value, out var floatKey))
                {
                    return floatKey;
                }

                return new NormalizedScalarKey("str", value);
            }

            private static bool IsNull(string value)
            {
                return value.Length == 0
                    || value == "~"
                    || value == "null"
                    || value == "Null"
                    || value == "NULL";
            }

            private static bool TryResolveBool(string value, out NormalizedScalarKey key)
            {
                switch (value)
                {
                    case "true":
                    case "True":
                    case "TRUE":
                        key = new NormalizedScalarKey("bool", "true");
                        return true;
                    case "false":
                    case "False":
                    case "FALSE":
                        key = new NormalizedScalarKey("bool", "false");
                        return true;
                    default:
                        key = default;
                        return false;
                }
            }

            private static bool TryResolveInt(string value, out NormalizedScalarKey key)
            {
                key = default;
                if (value.Length == 0)
                {
                    return false;
                }

                if (value.StartsWith("0x", StringComparison.Ordinal))
                {
                    return TryParseHex(value, 2, ref key);
                }

                if (value.StartsWith("0o", StringComparison.Ordinal))
                {
                    return TryParseOctal(value, ref key);
                }

                var digits = value;
                if (value[0] == '+' || value[0] == '-')
                {
                    digits = value.Substring(1);
                }

                if (digits.Length == 0 || !AllDecimalDigits(digits))
                {
                    return false;
                }

                var number = BigInteger.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                key = new NormalizedScalarKey("int", number.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            private static bool TryParseHex(string value, int prefixLength, ref NormalizedScalarKey key)
            {
                var digits = value.Substring(prefixLength);
                if (digits.Length == 0)
                {
                    return false;
                }

                foreach (var c in digits)
                {
                    var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                    if (!isHex)
                    {
                        return false;
                    }
                }

                // Prepend "0" so that BigInteger does not interpret the value as two's complement.
                var number = BigInteger.Parse("0" + digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                key = new NormalizedScalarKey("int", number.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            private static bool TryParseOctal(string value, ref NormalizedScalarKey key)
            {
                var digits = value.Substring(2);
                if (digits.Length == 0)
                {
                    return false;
                }

                var number = BigInteger.Zero;
                foreach (var c in digits)
                {
                    if (c < '0' || c > '7')
                    {
                        return false;
                    }

                    number = number * 8 + (c - '0');
                }

                key = new NormalizedScalarKey("int", number.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            private static bool TryResolveFloat(string value, out NormalizedScalarKey key)
            {
                key = default;
                var body = value;
                var negative = false;
                if (body.Length > 0 && (body[0] == '+' || body[0] == '-'))
                {
                    negative = body[0] == '-';
                    body = body.Substring(1);
                }

                if (body == ".inf" || body == ".Inf" || body == ".INF")
                {
                    key = new NormalizedScalarKey("float", negative ? "-.inf" : ".inf");
                    return true;
                }

                if (body == ".nan" || body == ".NaN" || body == ".NAN")
                {
                    key = new NormalizedScalarKey("float", ".nan");
                    return true;
                }

                if (!LooksLikeFloat(value))
                {
                    return false;
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return false;
                }

                key = new NormalizedScalarKey("float", number.ToString("R", CultureInfo.InvariantCulture));
                return true;
            }

            private static bool LooksLikeFloat(string value)
            {
                var index = 0;
                if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                {
                    index++;
                }

                var mantissaDigits = 0;
                var hasDot = false;
                while (index < value.Length)
                {
                    var c = value[index];
                    if (c >= '0' && c <= '9')
                    {
                        mantissaDigits++;
                    }
                    else if (c == '.' && !hasDot)
                    {
                        hasDot = true;
                    }
                    else
                    {
                        break;
                    }

                    index++;
                }

                if (mantissaDigits == 0)
                {
                    return false;
                }

                var hasExponent = false;
                if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
                {
                    hasExponent = true;
                    index++;
                    if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                    {
                        index++;
                    }

                    var exponentDigits = 0;
                    while (index < value.Length && value[index] >= '0' && value[index] <= '9')
                    {
                        exponentDigits++;
                        index++;
                    }

                    if (exponentDigits == 0)
                    {
                        return false;
                    }
                }

                // Must consume the whole value and look like a float (not an int).
                return index == value.Length && (hasDot || hasExponent);
            }

            private static bool AllDecimalDigits(string value)
            {
                foreach (var c in value)
                {
                    if (c < '0' || c > '9')
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
