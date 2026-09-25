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

namespace YamlDotNet.Serialization.Preflight
{
    /// <summary>
    /// Describes a single problem detected by the deserialization preflight validation.
    /// </summary>
    public sealed class PreflightValidationError
    {
        /// <summary>
        /// A stable, machine-readable code that identifies the kind of problem.
        /// See <see cref="PreflightValidationErrorCodes"/> for the well-known codes.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A human-readable description of the problem.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The position where the offending event starts.
        /// </summary>
        public Mark Start { get; }

        /// <summary>
        /// The position where the offending event ends.
        /// </summary>
        public Mark End { get; }

        /// <summary>
        /// The start of the related location, when the problem involves two positions
        /// (for example, the anchor definition that an incompatible alias refers to).
        /// <see cref="Mark.Empty"/> when there is no related location.
        /// </summary>
        public Mark RelatedStart { get; }

        /// <summary>
        /// The end of the related location, or <see cref="Mark.Empty"/> when there is none.
        /// </summary>
        public Mark RelatedEnd { get; }

        /// <summary>
        /// A human-readable description of the related location, when present.
        /// </summary>
        public string? RelatedMessage { get; }

        public PreflightValidationError(string code, string message, Mark start, Mark end)
            : this(code, message, start, end, Mark.Empty, Mark.Empty, null)
        {
        }

        public PreflightValidationError(string code, string message, Mark start, Mark end, Mark relatedStart, Mark relatedEnd, string? relatedMessage)
        {
            Code = code;
            Message = message;
            Start = start;
            End = end;
            RelatedStart = relatedStart;
            RelatedEnd = relatedEnd;
            RelatedMessage = relatedMessage;
        }

        public override string ToString()
        {
            var related = RelatedMessage is null ? string.Empty : $" ({RelatedMessage}: {RelatedStart} - {RelatedEnd})";
            return $"[{Code}] ({Start}) - ({End}): {Message}{related}";
        }
    }

    /// <summary>
    /// Well-known codes reported in <see cref="PreflightValidationError.Code"/>.
    /// </summary>
    public static class PreflightValidationErrorCodes
    {
        /// <summary>The configured limit of buffered events was exceeded.</summary>
        public const string EventLimitExceeded = nameof(EventLimitExceeded);

        /// <summary>The configured limit of nesting depth was exceeded.</summary>
        public const string NestingLimitExceeded = nameof(NestingLimitExceeded);

        /// <summary>A tag was encountered that is not registered in the tag mappings.</summary>
        public const string UnknownTag = nameof(UnknownTag);

        /// <summary>A tag resolved to a type that is not assignable to the target type at that position.</summary>
        public const string IncompatibleTaggedType = nameof(IncompatibleTaggedType);

        /// <summary>The target type is abstract or an interface and no tag determines the concrete type.</summary>
        public const string AmbiguousPolymorphicType = nameof(AmbiguousPolymorphicType);

        /// <summary>An alias refers to an anchor whose type is not compatible with the target type at the alias position.</summary>
        public const string IncompatibleAlias = nameof(IncompatibleAlias);

        /// <summary>An alias refers to an anchor that has not been defined yet.</summary>
        public const string UnresolvableAlias = nameof(UnresolvableAlias);

        /// <summary>The same anchor name is defined more than once.</summary>
        public const string DuplicateAnchor = nameof(DuplicateAnchor);

        /// <summary>The event stream does not have the expected structure.</summary>
        public const string UnexpectedEvent = nameof(UnexpectedEvent);
    }
}
