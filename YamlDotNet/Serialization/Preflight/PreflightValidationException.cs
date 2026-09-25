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
using System.Text;
using YamlDotNet.Core;

namespace YamlDotNet.Serialization.Preflight
{
    /// <summary>
    /// The exception that is thrown when the deserialization preflight validation rejects the input.
    /// When this exception is thrown, no object has been constructed and no property setter has been invoked.
    /// </summary>
    public sealed class PreflightValidationException : YamlException
    {
        /// <summary>
        /// The problems that were detected during preflight validation.
        /// </summary>
        public IReadOnlyList<PreflightValidationError> Errors { get; }

        public PreflightValidationException(PreflightValidationError error)
            : this(new PreflightValidationError[] { error ?? throw new ArgumentNullException(nameof(error)) })
        {
        }

        public PreflightValidationException(IReadOnlyList<PreflightValidationError> errors)
            : base(GetPrimaryStart(errors), GetPrimaryEnd(errors), BuildMessage(errors))
        {
            Errors = errors;
        }

        private static Mark GetPrimaryStart(IReadOnlyList<PreflightValidationError> errors)
        {
            return errors.Count > 0 ? errors[0].Start : Mark.Empty;
        }

        private static Mark GetPrimaryEnd(IReadOnlyList<PreflightValidationError> errors)
        {
            return errors.Count > 0 ? errors[0].End : Mark.Empty;
        }

        private static string BuildMessage(IReadOnlyList<PreflightValidationError> errors)
        {
            if (errors.Count == 0)
            {
                return "Deserialization preflight validation failed.";
            }

            var builder = new StringBuilder("Deserialization preflight validation failed:");
            foreach (var error in errors)
            {
                builder.AppendLine();
                builder.Append("  ").Append(error.ToString());
            }
            return builder.ToString();
        }
    }
}
