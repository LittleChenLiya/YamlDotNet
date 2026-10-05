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
    /// Represents the result of selecting a <see cref="ScalarStyle"/> for a string value,
    /// including the reason why that style was chosen.
    /// </summary>
    public sealed class ScalarStyleSelection
    {
        /// <summary>
        /// Gets the selected scalar style.
        /// </summary>
        public ScalarStyle Style { get; }

        /// <summary>
        /// Gets the reason why <see cref="Style"/> was selected.
        /// </summary>
        public StringStyleReason Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScalarStyleSelection"/> class.
        /// </summary>
        /// <param name="style">The selected scalar style.</param>
        /// <param name="reason">The reason why the style was selected.</param>
        public ScalarStyleSelection(ScalarStyle style, StringStyleReason reason)
        {
            Style = style;
            Reason = reason;
        }

        /// <summary>
        /// Deconstructs the selection into its style and reason.
        /// </summary>
        public void Deconstruct(out ScalarStyle style, out StringStyleReason reason)
        {
            style = Style;
            reason = Reason;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Style} ({Reason})";
    }
}
