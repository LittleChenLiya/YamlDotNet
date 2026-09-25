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

namespace YamlDotNet.Core
{
    /// <summary>
    /// Configures the behavior of <see cref="DuplicateKeyDetectorParser"/>.
    /// </summary>
    public sealed class DuplicateKeyDetectorOptions
    {
        /// <summary>
        /// The default node budget. See <see cref="NodeBudget"/>.
        /// </summary>
        public const int DefaultNodeBudget = 4096;

        /// <summary>
        /// Gets or sets the normalizer that is used to compare scalar keys.
        /// Defaults to <see cref="ScalarKeyNormalizers.Ordinal"/>, which compares the raw
        /// string values. Use <see cref="ScalarKeyNormalizers.CoreSchema"/> to resolve
        /// plain scalars according to the YAML 1.2 Core Schema before comparing them,
        /// or provide a custom <see cref="IScalarKeyNormalizer"/> implementation.
        /// </summary>
        public IScalarKeyNormalizer KeyNormalizer { get; set; } = ScalarKeyNormalizers.Ordinal;

        /// <summary>
        /// Gets or sets the maximum number of nodes that are canonicalized per document
        /// while tracking keys and anchors. The budget prevents pathological alias graphs
        /// from consuming unbounded resources; the detector never expands the whole alias
        /// graph because each anchored node is canonicalized at most once. When the budget
        /// is exceeded, a <see cref="DuplicateKeyDiagnosticKind.NodeBudgetExceeded"/>
        /// diagnostic is reported and duplicate detection is disabled for the remainder
        /// of the current document. Defaults to <see cref="DefaultNodeBudget"/>.
        /// </summary>
        public int NodeBudget { get; set; } = DefaultNodeBudget;

        /// <summary>
        /// Gets or sets an optional callback that is invoked for every diagnostic as soon
        /// as it is detected. All diagnostics are also collected in
        /// <see cref="DuplicateKeyDetectorParser.Diagnostics"/>.
        /// </summary>
        public Action<DuplicateKeyDiagnostic>? OnDiagnostic { get; set; }
    }
}
