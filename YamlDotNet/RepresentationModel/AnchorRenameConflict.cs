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

using YamlDotNet.Core;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Describes an alias whose binding would change if an anchor rename was applied.
    /// </summary>
    public sealed class AnchorRenameConflict
    {
        /// <summary>
        /// Gets the position where the conflicting alias token starts.
        /// </summary>
        public Mark Start { get; }

        /// <summary>
        /// Gets the position where the conflicting alias token ends.
        /// </summary>
        public Mark End { get; }

        /// <summary>
        /// Gets the name of the conflicting alias (without the '*' indicator).
        /// </summary>
        public string AliasName { get; }

        /// <summary>
        /// Gets a human-readable description of the conflict.
        /// </summary>
        public string Message { get; }

        internal AnchorRenameConflict(Mark start, Mark end, string aliasName, string message)
        {
            Start = start;
            End = end;
            AliasName = aliasName;
            Message = message;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Message} (alias '*{AliasName}' at line {Start.Line}, column {Start.Column})";
    }
}
