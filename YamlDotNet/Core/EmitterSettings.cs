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
    public sealed class EmitterSettings
    {
        /// <summary>
        /// The preferred indentation.
        /// </summary>
        public int BestIndent { get; } = 2;

        /// <summary>
        /// The preferred text width.
        /// </summary>
        public int BestWidth { get; } = int.MaxValue;

        /// <summary>
        /// New line characters.
        /// </summary>
        public string NewLine { get; } = Environment.NewLine;

        /// <summary>
        /// If true, write the output in canonical form.
        /// </summary>
        public bool IsCanonical { get; }

        /// <summary>
        /// If true, write output without anchor names.
        /// </summary>
        public bool SkipAnchorName { get; private set; }

        /// <summary>
        /// The maximum allowed length for simple keys.
        /// </summary>
        /// <remarks>
        /// The specifiction mandates 1024 characters, but any desired value may be used.
        /// </remarks>
        public int MaxSimpleKeyLength { get; } = 1024;

        /// <summary>
        /// Indent sequences. The default is to not indent.
        /// </summary>
        public bool IndentSequences { get; }

        /// <summary>
        /// If true, then 4-byte UTF-32 characters are broken into two 2-byte code-points.
        /// </summary>
        /// <remarks>
        /// This ensures compatibility with JSON format, as it does not allow '\Uxxxxxxxxx'
        /// and instead expects two escaped 2-byte character '\uxxxx\uxxxx'.
        /// </remarks>
        public bool UseUtf16SurrogatePairs { get; }

        /// <summary>
        /// If true, the <see cref="StringStyleSelector"/> is used to select a safe scalar style
        /// for string values based on the target schema, ensuring that the emitted scalar
        /// round-trips to the original string when re-parsed.
        /// </summary>
        /// <remarks>
        /// The default is <see langword="false"/>, which preserves the legacy behavior where
        /// the emitter chooses the style (folded for multiline, plain otherwise).
        /// </remarks>
        public bool UseStringStyleSelector { get; }

        public static readonly EmitterSettings Default = new EmitterSettings();

        public EmitterSettings()
        {
        }

        public EmitterSettings(int bestIndent, int bestWidth, bool isCanonical, int maxSimpleKeyLength, bool skipAnchorName = false, bool indentSequences = false, string? newLine = null, bool useUtf16SurrogatePairs = false, bool useStringStyleSelector = false)
        {
            if (bestIndent < 2 || bestIndent > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(bestIndent), $"BestIndent must be between 2 and 9, inclusive");
            }

            if (bestWidth <= bestIndent * 2)
            {
                throw new ArgumentOutOfRangeException(nameof(bestWidth), "BestWidth must be greater than BestIndent x 2.");
            }

            if (maxSimpleKeyLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSimpleKeyLength), "MaxSimpleKeyLength must be >= 0");
            }

            BestIndent = bestIndent;
            BestWidth = bestWidth;
            IsCanonical = isCanonical;
            MaxSimpleKeyLength = maxSimpleKeyLength;
            SkipAnchorName = skipAnchorName;
            IndentSequences = indentSequences;
            NewLine = newLine ?? Environment.NewLine;
            UseUtf16SurrogatePairs = useUtf16SurrogatePairs;
            UseStringStyleSelector = useStringStyleSelector;
        }

        public EmitterSettings WithBestIndent(int bestIndent)
        {
            return new EmitterSettings(
                bestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithBestWidth(int bestWidth)
        {
            return new EmitterSettings(
                BestIndent,
                bestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithMaxSimpleKeyLength(int maxSimpleKeyLength)
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                maxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithNewLine(string newLine)
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                newLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings Canonical()
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                true,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithoutAnchorName()
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                true,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithIndentedSequences()
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                true,
                NewLine,
                UseUtf16SurrogatePairs,
                UseStringStyleSelector
            );
        }

        public EmitterSettings WithUtf16SurrogatePairs()
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                true,
                UseStringStyleSelector
            );
        }

        /// <summary>
        /// Returns a new <see cref="EmitterSettings"/> with <see cref="UseStringStyleSelector"/> enabled.
        /// When enabled, the <see cref="StringStyleSelector"/> selects a safe scalar style for
        /// string values based on the target schema.
        /// </summary>
        public EmitterSettings WithStringStyleSelector()
        {
            return new EmitterSettings(
                BestIndent,
                BestWidth,
                IsCanonical,
                MaxSimpleKeyLength,
                SkipAnchorName,
                IndentSequences,
                NewLine,
                UseUtf16SurrogatePairs,
                true
            );
        }
    }
}
