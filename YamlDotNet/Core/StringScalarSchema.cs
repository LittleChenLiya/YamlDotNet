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
    /// Identifies the YAML schema that determines which plain scalars would be
    /// implicitly resolved as a non-string type (null, boolean, number, ...).
    /// Used by <see cref="StringStyleSelector"/> to decide when a string must be
    /// quoted in order to round-trip as a string.
    /// </summary>
    public enum StringScalarSchema
    {
        /// <summary>
        /// The YAML failsafe schema: scalars are strings unless explicitly tagged.
        /// Only the empty scalar and "~" are still treated as null, matching the
        /// behavior of this library's deserializer.
        /// </summary>
        Failsafe,

        /// <summary>
        /// The YAML 1.2 core schema: "null", "true"/"false", decimal / hexadecimal /
        /// octal integers, floating point numbers and ".inf" / ".nan" resolve to
        /// non-string types. Words such as "yes" or "on" remain plain strings.
        /// </summary>
        Core,

        /// <summary>
        /// YAML 1.1 resolution, which matches the default behavior of this library's
        /// deserializer: additionally "y"/"n"/"yes"/"no"/"on"/"off" are booleans,
        /// integers may be binary, octal with a leading zero ("012") or sexagesimal,
        /// and dates are timestamps.
        /// </summary>
        Yaml11,
    }
}
