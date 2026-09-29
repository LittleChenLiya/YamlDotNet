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
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization
{
    /// <summary>
    /// Deserializes objects from the YAML format.
    /// To customize the behavior of <see cref="Deserializer" />,
    /// use the <see cref="DeserializerBuilder" /> class.
    /// </summary>
    public sealed class Deserializer : IDeserializer
    {
        private readonly IValueDeserializer valueDeserializer;

        /// <summary>
        /// Initializes a new instance of <see cref="Deserializer" /> using the default configuration.
        /// </summary>
        /// <remarks>
        /// To customize the behavior of the deserializer, use <see cref="DeserializerBuilder" />.
        /// </remarks>
        public Deserializer()
            : this(new DeserializerBuilder().BuildValueDeserializer())
        {
        }

        /// <remarks>
        /// This constructor is private to discourage its use.
        /// To invoke it, call the <see cref="FromValueDeserializer"/> method.
        /// </remarks>
        private Deserializer(IValueDeserializer valueDeserializer)
        {
            this.valueDeserializer = valueDeserializer ?? throw new ArgumentNullException(nameof(valueDeserializer));
        }

        /// <summary>
        /// Creates a new <see cref="Deserializer" /> that uses the specified <see cref="IValueDeserializer" />.
        /// This method is available for advanced scenarios. The preferred way to customize the behavior of the
        /// deserializer is to use <see cref="DeserializerBuilder" />.
        /// </summary>
        public static Deserializer FromValueDeserializer(IValueDeserializer valueDeserializer)
        {
            return new Deserializer(valueDeserializer);
        }

        public T Deserialize<T>(string input)
        {
            using var reader = new StringReader(input);
            return Deserialize<T>(reader);
        }

        public T Deserialize<T>(TextReader input)
        {
            return Deserialize<T>(new Parser(input));
        }

        public T Deserialize<T>(IParser parser)
        {
            return (T)Deserialize(parser, typeof(T))!; // We really want an exception if we are trying to deserialize null into a non-nullable type
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// All other documents are parsed to validate their syntax, but no objects are constructed for them.
        /// </summary>
        /// <param name="input">The <see cref="string" /> containing the YAML documents.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        public T Deserialize<T>(string input, int documentIndex)
        {
            using var reader = new StringReader(input);
            return Deserialize<T>(reader, documentIndex);
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// All other documents are parsed to validate their syntax, but no objects are constructed for them.
        /// </summary>
        /// <param name="input">The <see cref="TextReader" /> from where to deserialize the object.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        public T Deserialize<T>(TextReader input, int documentIndex)
        {
            return Deserialize<T>(new Parser(input), documentIndex);
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// All other documents are parsed to validate their syntax, but no objects are constructed for them.
        /// </summary>
        /// <param name="parser">The <see cref="IParser" /> from where to deserialize the object.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        public T Deserialize<T>(IParser parser, int documentIndex)
        {
            return (T)Deserialize(parser, typeof(T), documentIndex)!; // We really want an exception if we are trying to deserialize null into a non-nullable type
        }

        public object? Deserialize(string input)
        {
            return Deserialize<object>(input);
        }

        public object? Deserialize(TextReader input)
        {
            return Deserialize<object>(input);
        }

        public object? Deserialize(IParser parser)
        {
            return Deserialize<object>(parser);
        }

        public object? Deserialize(string input, Type type)
        {
            using var reader = new StringReader(input);
            return Deserialize(reader, type);
        }

        public object? Deserialize(TextReader input, Type type)
        {
            return Deserialize(new Parser(input), type);
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// All other documents are parsed to validate their syntax, but no objects are constructed for them.
        /// </summary>
        /// <param name="input">The <see cref="string" /> containing the YAML documents.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        public object? Deserialize(string input, Type type, int documentIndex)
        {
            using var reader = new StringReader(input);
            return Deserialize(reader, type, documentIndex);
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// All other documents are parsed to validate their syntax, but no objects are constructed for them.
        /// </summary>
        /// <param name="input">The <see cref="TextReader" /> from where to deserialize the object.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        public object? Deserialize(TextReader input, Type type, int documentIndex)
        {
            return Deserialize(new Parser(input), type, documentIndex);
        }

        /// <summary>
        /// Deserializes an object of the specified type from the document at the specified zero-based index.
        /// Every document is fully parsed, so syntax errors in any document are reported,
        /// but only the selected document is deserialized: no objects are constructed,
        /// no custom converters are invoked and no alias state is kept for the documents
        /// that are skipped.
        /// </summary>
        /// <param name="parser">The <see cref="IParser" /> from where to deserialize the object.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <param name="documentIndex">The zero-based index of the document to deserialize.</param>
        /// <returns>Returns the deserialized object, or null if the selected document is empty or does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="documentIndex" /> is negative.</exception>
        public object? Deserialize(IParser parser, Type type, int documentIndex)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (documentIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentIndex), documentIndex, "The document index must be greater than or equal to zero.");
            }

            var hasStreamStart = parser.TryConsume<StreamStart>(out var _);

            object? result = null;
            var currentDocumentIndex = 0;
            while (parser.TryConsume<DocumentStart>(out var _))
            {
                if (!parser.Accept<DocumentEnd>(out var _) && !parser.Accept<StreamEnd>(out var _))
                {
                    if (currentDocumentIndex == documentIndex)
                    {
                        using var state = new SerializerState();
                        result = valueDeserializer.DeserializeValue(parser, type, state, valueDeserializer);
                        state.OnDeserialization();
                    }
                    else
                    {
                        // Fully parse the document to validate its syntax,
                        // but do not construct any object for it.
                        parser.SkipThisAndNestedEvents();
                    }
                }

                parser.Consume<DocumentEnd>();
                currentDocumentIndex++;
            }

            if (hasStreamStart)
            {
                parser.Consume<StreamEnd>();
            }

            return result;
        }

        /// <summary>
        /// Deserializes an object of the specified type.
        /// </summary>
        /// <param name="parser">The <see cref="IParser" /> from where to deserialize the object.</param>
        /// <param name="type">The static type of the object to deserialize.</param>
        /// <returns>Returns the deserialized object.</returns>
        public object? Deserialize(IParser parser, Type type)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var hasStreamStart = parser.TryConsume<StreamStart>(out var _);

            var hasDocumentStart = parser.TryConsume<DocumentStart>(out var _);

            object? result = null;
            if (!parser.Accept<DocumentEnd>(out var _) && !parser.Accept<StreamEnd>(out var _))
            {
                using var state = new SerializerState();
                result = valueDeserializer.DeserializeValue(parser, type, state, valueDeserializer);
                state.OnDeserialization();
            }

            if (hasDocumentStart)
            {
                parser.Consume<DocumentEnd>();
            }

            if (hasStreamStart)
            {
                parser.Consume<StreamEnd>();
            }

            return result;
        }
    }
}
