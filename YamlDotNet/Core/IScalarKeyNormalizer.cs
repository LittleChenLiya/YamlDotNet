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

using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// Normalizes a scalar event into a comparable <see cref="NormalizedScalarKey"/>.
    /// Implementations decide how scalar mapping keys are compared, for example by raw string
    /// value or by resolving the scalar according to a schema.
    /// </summary>
    public interface IScalarKeyNormalizer
    {
        /// <summary>
        /// Normalizes the specified scalar event into a comparable key.
        /// </summary>
        /// <param name="scalar">The scalar event to normalize.</param>
        /// <returns>The normalized form of the scalar.</returns>
        NormalizedScalarKey Normalize(Scalar scalar);
    }
}
