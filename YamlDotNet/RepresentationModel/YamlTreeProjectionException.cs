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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// The exception that is thrown when a <see cref="YamlNode"/> graph cannot be projected into a tree.
    /// </summary>
    public sealed class YamlTreeProjectionException : InvalidOperationException
    {
        /// <summary>
        /// Gets the reason why the projection failed.
        /// </summary>
        public YamlTreeProjectionStatus Status { get; }

        /// <summary>
        /// Gets the node budget that was in effect when the projection was attempted.
        /// </summary>
        public int MaxNodes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlTreeProjectionException"/> class.
        /// </summary>
        /// <param name="status">The reason why the projection failed. Must not be <see cref="YamlTreeProjectionStatus.Success"/>.</param>
        /// <param name="maxNodes">The node budget that was in effect.</param>
        public YamlTreeProjectionException(YamlTreeProjectionStatus status, int maxNodes)
            : base(CreateMessage(status, maxNodes))
        {
            if (status == YamlTreeProjectionStatus.Success)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "A projection exception cannot report success.");
            }

            Status = status;
            MaxNodes = maxNodes;
        }

        private static string CreateMessage(YamlTreeProjectionStatus status, int maxNodes)
        {
            return status switch
            {
                YamlTreeProjectionStatus.BudgetExceeded =>
                    $"The node graph cannot be projected because its expansion requires more than the budget of {maxNodes} nodes.",
                YamlTreeProjectionStatus.CycleDetected =>
                    "The node graph cannot be projected because it contains a cycle.",
                _ =>
                    "The node graph cannot be projected.",
            };
        }
    }
}
