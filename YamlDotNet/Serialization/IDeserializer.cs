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
using System.IO;
using YamlDotNet.Core;

namespace YamlDotNet.Serialization
{
    public interface IDeserializer
    {
        T Deserialize<T>(string input);
        T Deserialize<T>(TextReader input);
        T Deserialize<T>(IParser parser);

        object? Deserialize(string input);
        object? Deserialize(TextReader input);
        object? Deserialize(IParser parser);
        object? Deserialize(string input, Type type);
        object? Deserialize(TextReader input, Type type);

        /// <summary>
        /// Deserializes an object of the specified type.
        /// </summary>
        /// <param name="parser">The <see cref="IParser" /> from where to deserialize the object.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <returns>Returns the deserialized object.</returns>
        object? Deserialize(IParser parser, Type type);

        /// <summary>
        /// Deserializes the document at the specified zero-based index from a YAML stream
        /// that may contain multiple documents.
        /// </summary>
        /// <param name="input">The YAML input.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object.</returns>
        /// <remarks>
        /// All documents are fully parsed, so syntax errors in skipped documents are still reported,
        /// but skipped documents do not construct objects, do not invoke type converters,
        /// and do not carry anchor/alias state into other documents.
        /// </remarks>
        T Deserialize<T>(string input, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        T Deserialize<T>(TextReader input, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        T Deserialize<T>(IParser parser, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        object? Deserialize(string input, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        object? Deserialize(TextReader input, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        object? Deserialize(IParser parser, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        object? Deserialize(string input, Type type, int documentIndex);

        /// <inheritdoc cref="Deserialize{T}(string, int)" />
        object? Deserialize(TextReader input, Type type, int documentIndex);

        /// <summary>
        /// Deserializes the document at the specified zero-based index from a YAML stream
        /// that may contain multiple documents.
        /// </summary>
        /// <param name="parser">The <see cref="IParser" /> from where to deserialize the document.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object.</returns>
        /// <remarks>
        /// All documents are fully parsed, so syntax errors in skipped documents are still reported,
        /// but skipped documents do not construct objects, do not invoke type converters,
        /// and do not carry anchor/alias state into other documents.
        /// </remarks>
        object? Deserialize(IParser parser, Type type, int documentIndex);
    }
}
