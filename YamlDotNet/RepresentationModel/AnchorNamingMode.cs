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
    /// Specifies how anchors are assigned to the nodes of a representation model when it is saved.
    /// </summary>
    public enum AnchorNamingMode
    {
        /// <summary>
        /// Nodes that are referenced more than once receive a randomly generated anchor name.
        /// This is the historical behavior of <see cref="YamlStream.Save(System.IO.TextWriter)"/>.
        /// </summary>
        Random = 0,

        /// <summary>
        /// Nodes that are referenced more than once (or that participate in a cycle) receive
        /// deterministic anchor names (<c>a1</c>, <c>a2</c>, ...) assigned in the order of their
        /// first appearance in a depth-first traversal of each document. Saving the same graph
        /// multiple times always produces identical output.
        /// </summary>
        Deterministic = 1,
    }
}
