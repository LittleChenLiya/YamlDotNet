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
using System.Collections;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Serialization.Preflight
{
    /// <summary>
    /// Performs the optional deserialization preflight validation. The whole event stream is
    /// buffered (up to a configurable limit) and checked against the target type and the
    /// registered tag mappings before any object is constructed:
    /// <list type="bullet">
    /// <item>Every tag must be registered in the tag mappings.</item>
    /// <item>The type resolved from a tag must be assignable to the target type at that position.</item>
    /// <item>Abstract or interface positions without a tag are rejected as ambiguous.</item>
    /// <item>Aliases must refer to previously defined anchors, and the anchored type must be
    /// compatible with the target type at each position where the alias is used.</item>
    /// <item>Duplicate anchor definitions are rejected.</item>
    /// </list>
    /// When validation fails, a <see cref="PreflightValidationException"/> is thrown and it is
    /// guaranteed that no constructor or property setter has been executed.
    /// </summary>
    public sealed class DeserializationPreflightValidator
    {
        private static readonly Type ObjectType = typeof(object);

        private readonly ITypeInspector typeInspector;
        private readonly IReadOnlyDictionary<TagName, Type> tagMappings;
        private readonly IReadOnlyDictionary<Type, Type> typeMappings;
        private readonly PreflightValidationOptions options;
        private readonly bool caseInsensitivePropertyMatching;

        public DeserializationPreflightValidator(
            ITypeInspector typeInspector,
            IReadOnlyDictionary<TagName, Type> tagMappings,
            IReadOnlyDictionary<Type, Type> typeMappings,
            PreflightValidationOptions options,
            bool caseInsensitivePropertyMatching = false)
        {
            this.typeInspector = typeInspector ?? throw new ArgumentNullException(nameof(typeInspector));
            this.tagMappings = tagMappings ?? throw new ArgumentNullException(nameof(tagMappings));
            this.typeMappings = typeMappings ?? throw new ArgumentNullException(nameof(typeMappings));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.caseInsensitivePropertyMatching = caseInsensitivePropertyMatching;

            if (options.MaxBufferedEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "The maximum number of buffered events must be a positive integer.");
            }

            if (options.MaxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "The maximum nesting depth must be a positive integer.");
            }
        }

        /// <summary>
        /// Buffers the whole event stream produced by <paramref name="parser"/> (up to the configured
        /// event limit) and validates the first document against <paramref name="expectedType"/>.
        /// </summary>
        /// <returns>The buffered events, which can be replayed with <see cref="BufferedEventParser"/>.</returns>
        /// <exception cref="PreflightValidationException">The input was rejected.</exception>
        public IReadOnlyList<ParsingEvent> BufferAndValidate(IParser parser, Type expectedType)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (expectedType == null)
            {
                throw new ArgumentNullException(nameof(expectedType));
            }

            var events = BufferEvents(parser);
            Validate(events, expectedType);
            return events;
        }

        /// <summary>
        /// Buffers the whole event stream produced by <paramref name="parser"/>, enforcing the
        /// configured limit on the total number of events.
        /// </summary>
        public IReadOnlyList<ParsingEvent> BufferEvents(IParser parser)
        {
            var events = new List<ParsingEvent>();
            while (parser.MoveNext())
            {
                var current = parser.Current;
                if (current == null)
                {
                    break;
                }

                events.Add(current);
                if (events.Count > options.MaxBufferedEvents)
                {
                    throw new PreflightValidationException(new PreflightValidationError(
                        PreflightValidationErrorCodes.EventLimitExceeded,
                        $"The input contains more than the configured maximum of {options.MaxBufferedEvents} events.",
                        current.Start,
                        current.End));
                }
            }

            return events;
        }

        /// <summary>
        /// Validates the first document of a buffered event stream against <paramref name="expectedType"/>.
        /// </summary>
        /// <exception cref="PreflightValidationException">The input was rejected.</exception>
        public void Validate(IReadOnlyList<ParsingEvent> events, Type expectedType)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (expectedType == null)
            {
                throw new ArgumentNullException(nameof(expectedType));
            }

            var walker = new Walker(this, events);
            walker.ValidateDocument(expectedType);
        }

        private static Type UnwrapNullable(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static bool IsAssignableTo(Type actualType, Type expectedType)
        {
            var expected = UnwrapNullable(expectedType);
            var actual = UnwrapNullable(actualType);
            return expected == ObjectType || expected.IsAssignableFrom(actual);
        }

        private static bool IsSupportedCollectionInterface(Type type, NodeEvent nodeEvent)
        {
            if (nodeEvent is SequenceStart)
            {
                if (type == typeof(IEnumerable) || type == typeof(IList) || type == typeof(ICollection))
                {
                    return true;
                }

                if (type.IsGenericType)
                {
                    var definition = type.GetGenericTypeDefinition();
                    return definition == typeof(IEnumerable<>)
                        || definition == typeof(IList<>)
                        || definition == typeof(ICollection<>)
                        || definition == typeof(IReadOnlyCollection<>)
                        || definition == typeof(IReadOnlyList<>);
                }
            }
            else if (nodeEvent is MappingStart)
            {
                if (type == typeof(IDictionary))
                {
                    return true;
                }

                if (type.IsGenericType)
                {
                    var definition = type.GetGenericTypeDefinition();
                    return definition == typeof(IDictionary<,>)
                        || definition == typeof(IReadOnlyDictionary<,>);
                }
            }

            return false;
        }

        private static Type GetElementType(Type collectionType)
        {
            var type = UnwrapNullable(collectionType);
            if (type.IsArray)
            {
                return type.GetElementType()!;
            }

            if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (type.IsGenericType && type.GetGenericArguments().Length == 1)
                {
                    return type.GetGenericArguments()[0];
                }

                foreach (var interfaceType in type.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        return interfaceType.GetGenericArguments()[0];
                    }
                }
            }

            return ObjectType;
        }

        private static bool TryGetDictionaryEntryTypes(Type type, out Type keyType, out Type valueType)
        {
            var unwrapped = UnwrapNullable(type);
            if (unwrapped != typeof(string))
            {
                if (unwrapped.IsGenericType && unwrapped.GetGenericArguments().Length == 2
                    && typeof(IDictionary).IsAssignableFrom(unwrapped))
                {
                    var arguments = unwrapped.GetGenericArguments();
                    keyType = arguments[0];
                    valueType = arguments[1];
                    return true;
                }

                foreach (var interfaceType in unwrapped.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        var arguments = interfaceType.GetGenericArguments();
                        keyType = arguments[0];
                        valueType = arguments[1];
                        return true;
                    }
                }

                if (typeof(IDictionary).IsAssignableFrom(unwrapped))
                {
                    keyType = ObjectType;
                    valueType = ObjectType;
                    return true;
                }
            }

            keyType = ObjectType;
            valueType = ObjectType;
            return false;
        }

        private sealed class AnchorInfo
        {
            public readonly Type Type;
            public readonly Mark Start;
            public readonly Mark End;

            public AnchorInfo(Type type, Mark start, Mark end)
            {
                Type = type;
                Start = start;
                End = end;
            }
        }

        private sealed class Walker
        {
            private readonly DeserializationPreflightValidator validator;
            private readonly IReadOnlyList<ParsingEvent> events;
            private readonly Dictionary<AnchorName, AnchorInfo> anchors = new Dictionary<AnchorName, AnchorInfo>();
            private int position;
            private int depth;

            public Walker(DeserializationPreflightValidator validator, IReadOnlyList<ParsingEvent> events)
            {
                this.validator = validator;
                this.events = events;
            }

            private ParsingEvent? Current => position < events.Count ? events[position] : null;

            public void ValidateDocument(Type expectedType)
            {
                // Mirror the event consumption of Deserializer.Deserialize: optional stream
                // and document start events, then a single root node.
                if (Current is StreamStart)
                {
                    position++;
                }

                if (Current is DocumentStart)
                {
                    position++;
                }

                if (Current == null || Current is DocumentEnd || Current is StreamEnd)
                {
                    // Empty document: nothing to validate.
                    return;
                }

                ValidateNode(expectedType);
            }

            private void ValidateNode(Type expectedType)
            {
                var current = Current;
                if (current == null)
                {
                    Fail(PreflightValidationErrorCodes.UnexpectedEvent, "Unexpected end of the event stream.", Mark.Empty, Mark.Empty);
                }

                depth++;
                if (depth > validator.options.MaxDepth)
                {
                    Fail(PreflightValidationErrorCodes.NestingLimitExceeded,
                        $"The input is nested deeper than the configured maximum of {validator.options.MaxDepth} levels.",
                        current!.Start, current.End);
                }

                try
                {
                    switch (current)
                    {
                        case AnchorAlias alias:
                            position++;
                            ValidateAlias(alias, expectedType);
                            break;

                        case Scalar scalar:
                            position++;
                            ResolveTaggedType(scalar, expectedType, enforceCompatibility: false);
                            break;

                        case SequenceStart sequenceStart:
                            ValidateSequence(sequenceStart, expectedType);
                            break;

                        case MappingStart mappingStart:
                            ValidateMapping(mappingStart, expectedType);
                            break;

                        default:
                            Fail(PreflightValidationErrorCodes.UnexpectedEvent,
                                $"Unexpected event '{current}'.",
                                current!.Start, current.End);
                            break;
                    }
                }
                finally
                {
                    depth--;
                }
            }

            private void ValidateSequence(SequenceStart sequenceStart, Type expectedType)
            {
                var resolvedType = ResolveTaggedType(sequenceStart, expectedType, enforceCompatibility: true);
                RegisterAnchor(sequenceStart, resolvedType);
                position++;

                var elementType = GetElementType(resolvedType);
                while (true)
                {
                    var current = Current;
                    if (current == null)
                    {
                        Fail(PreflightValidationErrorCodes.UnexpectedEvent, "Unterminated sequence.", sequenceStart.Start, sequenceStart.End);
                    }

                    if (current is SequenceEnd)
                    {
                        position++;
                        return;
                    }

                    ValidateNode(elementType);
                }
            }

            private void ValidateMapping(MappingStart mappingStart, Type expectedType)
            {
                var resolvedType = ResolveTaggedType(mappingStart, expectedType, enforceCompatibility: true);
                RegisterAnchor(mappingStart, resolvedType);
                position++;

                var isDictionary = TryGetDictionaryEntryTypes(resolvedType, out var keyType, out var valueType);
                while (true)
                {
                    var current = Current;
                    if (current == null)
                    {
                        Fail(PreflightValidationErrorCodes.UnexpectedEvent, "Unterminated mapping.", mappingStart.Start, mappingStart.End);
                    }

                    if (current is MappingEnd)
                    {
                        position++;
                        return;
                    }

                    string? keyName = current is Scalar keyScalar ? keyScalar.Value : null;
                    ValidateNode(isDictionary ? keyType : ObjectType);

                    var memberType = valueType;
                    if (!isDictionary && keyName != null)
                    {
                        memberType = GetMemberType(resolvedType, keyName);
                    }

                    ValidateNode(memberType);
                }
            }

            private Type GetMemberType(Type resolvedType, string memberName)
            {
                var type = UnwrapNullable(resolvedType);
                if (type == ObjectType || type.IsInterface || type.IsAbstract)
                {
                    return ObjectType;
                }

                try
                {
                    var property = validator.typeInspector.GetProperty(type, null, memberName, true, validator.caseInsensitivePropertyMatching);
                    return property?.Type ?? ObjectType;
                }
                catch
                {
                    return ObjectType;
                }
            }

            private void ValidateAlias(AnchorAlias alias, Type expectedType)
            {
                if (!anchors.TryGetValue(alias.Value, out var anchor))
                {
                    Fail(PreflightValidationErrorCodes.UnresolvableAlias,
                        $"Alias '{alias.Value}' refers to anchor '{alias.Value}' which has not been defined.",
                        alias.Start, alias.End);
                }

                if (!IsAssignableTo(anchor!.Type, expectedType))
                {
                    Fail(PreflightValidationErrorCodes.IncompatibleAlias,
                        $"Alias '{alias.Value}' refers to an anchor of type '{anchor.Type.FullName}' which is not compatible with the expected type '{expectedType.FullName}' at this position.",
                        alias.Start, alias.End,
                        anchor.Start, anchor.End,
                        $"Anchor '{alias.Value}' is defined here");
                }
            }

            private Type ResolveTaggedType(NodeEvent nodeEvent, Type expectedType, bool enforceCompatibility)
            {
                var resolvedType = expectedType;
                if (!nodeEvent.Tag.IsEmpty && !nodeEvent.Tag.IsNonSpecific)
                {
                    if (!validator.tagMappings.TryGetValue(nodeEvent.Tag, out var mappedType))
                    {
                        Fail(PreflightValidationErrorCodes.UnknownTag,
                            $"Tag '{nodeEvent.Tag}' is not registered in the tag mappings.",
                            nodeEvent.Start, nodeEvent.End);
                    }

                    resolvedType = mappedType!;
                }
                else
                {
                    var effectiveType = UnwrapNullable(expectedType);
                    if (validator.typeMappings.TryGetValue(effectiveType, out var concreteType))
                    {
                        resolvedType = concreteType;
                    }
                    else if (effectiveType != ObjectType
                        && (effectiveType.IsAbstract || effectiveType.IsInterface)
                        && !IsSupportedCollectionInterface(effectiveType, nodeEvent))
                    {
                        Fail(PreflightValidationErrorCodes.AmbiguousPolymorphicType,
                            $"Cannot determine the concrete type for the abstract or interface type '{effectiveType.FullName}' without a tag.",
                            nodeEvent.Start, nodeEvent.End);
                    }
                }

                if (enforceCompatibility && !IsAssignableTo(resolvedType, expectedType))
                {
                    Fail(PreflightValidationErrorCodes.IncompatibleTaggedType,
                        $"The type '{resolvedType.FullName}' is not compatible with the expected type '{expectedType.FullName}' at this position.",
                        nodeEvent.Start, nodeEvent.End);
                }

                return resolvedType;
            }

            private void RegisterAnchor(NodeEvent nodeEvent, Type resolvedType)
            {
                if (nodeEvent.Anchor.IsEmpty)
                {
                    return;
                }

                if (anchors.TryGetValue(nodeEvent.Anchor, out var existing))
                {
                    Fail(PreflightValidationErrorCodes.DuplicateAnchor,
                        $"Anchor '{nodeEvent.Anchor}' is defined more than once.",
                        nodeEvent.Start, nodeEvent.End,
                        existing.Start, existing.End,
                        $"Anchor '{nodeEvent.Anchor}' was first defined here");
                }

                anchors.Add(nodeEvent.Anchor, new AnchorInfo(resolvedType, nodeEvent.Start, nodeEvent.End));
            }

            private static void Fail(string code, string message, Mark start, Mark end)
            {
                throw new PreflightValidationException(new PreflightValidationError(code, message, start, end));
            }

            private static void Fail(string code, string message, Mark start, Mark end, Mark relatedStart, Mark relatedEnd, string relatedMessage)
            {
                throw new PreflightValidationException(new PreflightValidationError(code, message, start, end, relatedStart, relatedEnd, relatedMessage));
            }
        }
    }
}
