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

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Represents the result of an anchor rename operation performed by <see cref="YamlAnchorEditor.RenameAnchor"/>.
    /// </summary>
    public sealed class AnchorRenameResult
    {
        private static readonly IReadOnlyList<AnchorRenameConflict> NoConflicts = Array.Empty<AnchorRenameConflict>();

        /// <summary>
        /// Gets the outcome of the operation.
        /// </summary>
        public AnchorRenameStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether the rename was applied.
        /// </summary>
        public bool Success => Status == AnchorRenameStatus.Success;

        /// <summary>
        /// Gets the full YAML text with the rename applied.
        /// Only the target anchor token and the alias tokens bound to it are modified;
        /// every other byte of the input is preserved.
        /// This is <see langword="null"/> when the operation did not succeed; partial text is never returned.
        /// </summary>
        public string? NewText { get; }

        /// <summary>
        /// Gets a human-readable description of the failure, or <see langword="null"/> on success.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the aliases whose binding would change if the rename was applied.
        /// Only populated when <see cref="Status"/> is <see cref="AnchorRenameStatus.Conflict"/>.
        /// </summary>
        public IReadOnlyList<AnchorRenameConflict> Conflicts { get; }

        private AnchorRenameResult(AnchorRenameStatus status, string? newText, string? errorMessage, IReadOnlyList<AnchorRenameConflict>? conflicts)
        {
            Status = status;
            NewText = newText;
            ErrorMessage = errorMessage;
            Conflicts = conflicts ?? NoConflicts;
        }

        internal static AnchorRenameResult Succeeded(string newText) =>
            new AnchorRenameResult(AnchorRenameStatus.Success, newText, null, null);

        internal static AnchorRenameResult Failed(AnchorRenameStatus status, string errorMessage) =>
            new AnchorRenameResult(status, null, errorMessage, null);

        internal static AnchorRenameResult Failed(AnchorRenameStatus status, string errorMessage, IReadOnlyList<AnchorRenameConflict> conflicts) =>
            new AnchorRenameResult(status, null, errorMessage, conflicts);
    }
}
