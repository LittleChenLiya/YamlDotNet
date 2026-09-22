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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Describes the outcome of an anchor rename operation performed by <see cref="YamlAnchorEditor"/>.
    /// </summary>
    public enum AnchorRenameStatus
    {
        /// <summary>The rename was applied.</summary>
        Success,

        /// <summary>The old or new anchor name is empty or contains characters that are not allowed in anchor names.</summary>
        InvalidAnchorName,

        /// <summary>The requested document index does not exist in the YAML stream.</summary>
        DocumentNotFound,

        /// <summary>The requested occurrence of the anchor does not exist in the target document.</summary>
        AnchorNotFound,

        /// <summary>The input text is not well-formed YAML.</summary>
        SyntaxError,

        /// <summary>
        /// Applying the rename would change the binding of at least one alias.
        /// The conflicting aliases are reported in <see cref="AnchorRenameResult.Conflicts"/>.
        /// </summary>
        Conflict,
    }
}
