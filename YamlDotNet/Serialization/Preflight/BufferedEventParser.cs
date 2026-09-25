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
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Serialization.Preflight
{
    /// <summary>
    /// An <see cref="IParser"/> that replays a previously buffered list of parsing events.
    /// Used to feed the events that were validated during preflight validation into the
    /// regular deserialization pipeline without parsing the input again.
    /// </summary>
    public sealed class BufferedEventParser : IParser
    {
        private readonly IReadOnlyList<ParsingEvent> events;
        private int position = -1;

        public BufferedEventParser(IReadOnlyList<ParsingEvent> events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>
        /// Gets the current event. Returns null before the first call to <see cref="MoveNext" />,
        /// and also after <see cref="MoveNext" /> returns false.
        /// </summary>
        public ParsingEvent? Current => position >= 0 && position < events.Count ? events[position] : null;

        /// <summary>
        /// Moves to the next event.
        /// </summary>
        /// <returns>Returns true if there are more events available, otherwise returns false.</returns>
        public bool MoveNext()
        {
            if (position + 1 >= events.Count)
            {
                return false;
            }

            position++;
            return true;
        }
    }
}

