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
    /// Describes the outcome of an attempt to project a <see cref="YamlNode"/> graph into a tree.
    /// </summary>
    public enum YamlTreeProjectionStatus
    {
        /// <summary>
        /// The projection succeeded and a result was produced.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The fully expanded tree would contain more nodes than the specified budget allows.
        /// No result was allocated.
        /// </summary>
        BudgetExceeded,

        /// <summary>
        /// The node graph contains a genuine cycle (a node that is its own ancestor)
        /// and therefore cannot be expanded into a finite tree.
        /// Shared references that do not form a cycle are not reported here.
        /// </summary>
        CycleDetected,
    }
}
