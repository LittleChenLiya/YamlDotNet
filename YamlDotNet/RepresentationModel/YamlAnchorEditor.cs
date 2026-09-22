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
using System.IO;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// Provides a text-based document editing API that renames a YAML anchor and the aliases
    /// bound to it without altering any other byte of the source document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edit is computed from the YamlDotNet parsing events, so only real anchor and alias
    /// tokens are modified. Text that merely looks like an anchor or alias inside comments,
    /// quoted scalars or plain scalars is never touched, and documents other than the target
    /// one are left untouched because anchor scopes are per-document.
    /// </para>
    /// <para>
    /// Because the same anchor name may be redefined within a single document, the target
    /// definition is identified by its ordinal occurrence. An alias always binds to the most
    /// recent definition that precedes it in the same document.
    /// </para>
    /// <para>
    /// The operation is purely textual: nodes are never expanded and re-serialized, which
    /// preserves formatting, comments, and the reference topology (shared nodes and cycles).
    /// Positions reported by the parser are character offsets into the original UTF-16 text,
    /// so CRLF line endings and non-ASCII content are handled correctly.
    /// </para>
    /// </remarks>
    public static class YamlAnchorEditor
    {
        private const string DisallowedAnchorCharacters = "[]{},";

        private sealed class AnchorDefinition
        {
            public string Name = string.Empty;
            public int Sequence;
            public Mark EventStart;
        }

        private sealed class AliasReference
        {
            public string Name = string.Empty;
            public int Sequence;
            public Mark Start = Mark.Empty;
            public Mark End = Mark.Empty;
            public int OriginalBinding = -1;
        }

        /// <summary>
        /// Renames one definition of an anchor in a single YAML document, together with every
        /// alias that is bound to that definition, leaving all other bytes of the input unchanged.
        /// </summary>
        /// <param name="yaml">The complete YAML text. It is not modified; the edited text is returned in the result.</param>
        /// <param name="documentIndex">The zero-based index of the document inside the YAML stream.</param>
        /// <param name="oldName">The current anchor name, without the '&amp;' indicator.</param>
        /// <param name="newName">The desired anchor name, without the '&amp;' indicator.</param>
        /// <param name="occurrence">
        /// The one-based occurrence of the definition of <paramref name="oldName"/> inside the target
        /// document, in YAML order. Defaults to the first definition.
        /// </param>
        /// <returns>
        /// An <see cref="AnchorRenameResult"/>. On failure no partial text is produced; inspect
        /// <see cref="AnchorRenameResult.Status"/>, <see cref="AnchorRenameResult.ErrorMessage"/> and
        /// <see cref="AnchorRenameResult.Conflicts"/> for details.
        /// </returns>
        /// <remarks>
        /// The rename is rejected when it would change the binding of any alias, for example when
        /// the new name would capture an existing alias or when a later definition of the new name
        /// would shadow the renamed anchor for one of its own aliases.
        /// </remarks>
        public static AnchorRenameResult RenameAnchor(string yaml, int documentIndex, string oldName, string newName, int occurrence = 1)
        {
            if (yaml == null)
            {
                throw new ArgumentNullException(nameof(yaml));
            }

            if (oldName == null)
            {
                throw new ArgumentNullException(nameof(oldName));
            }

            if (newName == null)
            {
                throw new ArgumentNullException(nameof(newName));
            }

            if (documentIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentIndex), "The document index must be greater than or equal to zero.");
            }

            if (occurrence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(occurrence), "The occurrence must be greater than or equal to one.");
            }

            if (!IsValidAnchorName(oldName))
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.InvalidAnchorName,
                    $"'{oldName}' is not a valid anchor name. Anchor names must be non-empty and must not contain whitespace or any of: {DisallowedAnchorCharacters}");
            }

            if (!IsValidAnchorName(newName))
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.InvalidAnchorName,
                    $"'{newName}' is not a valid anchor name. Anchor names must be non-empty and must not contain whitespace or any of: {DisallowedAnchorCharacters}");
            }

            var definitions = new List<AnchorDefinition>();
            var aliases = new List<AliasReference>();
            var targetDefinitionId = -1;
            var documentCount = 0;

            try
            {
                CollectAnchors(yaml, documentIndex, oldName, occurrence, definitions, aliases, ref targetDefinitionId, ref documentCount);
            }
            catch (Exception ex) when (ex is YamlException || ex is InvalidOperationException)
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.SyntaxError,
                    $"The input is not valid YAML: {ex.Message}");
            }

            if (documentIndex >= documentCount)
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.DocumentNotFound,
                    $"The YAML stream contains {documentCount} document(s); there is no document at index {documentIndex}.");
            }

            if (targetDefinitionId < 0)
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.AnchorNotFound,
                    $"Document {documentIndex} does not contain occurrence {occurrence} of the anchor '&{oldName}'.");
            }

            var conflicts = FindBindingConflicts(definitions, aliases, targetDefinitionId, newName);
            if (conflicts.Count > 0)
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.Conflict,
                    $"Renaming '&{oldName}' to '&{newName}' would change the binding of {conflicts.Count} alias(es).",
                    conflicts);
            }

            string newText;
            try
            {
                newText = ApplyRename(yaml, definitions, aliases, targetDefinitionId, oldName, newName);
            }
            catch (InvalidOperationException ex)
            {
                return AnchorRenameResult.Failed(AnchorRenameStatus.SyntaxError, ex.Message);
            }

            // Safety net: the edited text must still parse, otherwise reject the whole operation.
            try
            {
                VerifyReparses(newText);
            }
            catch (Exception ex) when (ex is YamlException || ex is InvalidOperationException)
            {
                return AnchorRenameResult.Failed(
                    AnchorRenameStatus.SyntaxError,
                    $"The rename would produce invalid YAML and was rejected: {ex.Message}");
            }

            return AnchorRenameResult.Succeeded(newText);
        }

        private static void CollectAnchors(
            string yaml,
            int targetDocumentIndex,
            string oldName,
            int occurrence,
            List<AnchorDefinition> definitions,
            List<AliasReference> aliases,
            ref int targetDefinitionId,
            ref int documentCount)
        {
            var parser = new Parser(new StringReader(yaml));
            var documentIndex = -1;
            var inTargetDocument = false;
            var sequence = 0;
            var oldNameSeen = 0;
            var currentDefinitions = new Dictionary<string, int>(StringComparer.Ordinal);

            while (parser.MoveNext())
            {
                var parsingEvent = parser.Current!;
                switch (parsingEvent)
                {
                    case DocumentStart _:
                        documentIndex++;
                        inTargetDocument = documentIndex == targetDocumentIndex;
                        currentDefinitions.Clear();
                        break;

                    case DocumentEnd _:
                        inTargetDocument = false;
                        break;

                    case AnchorAlias alias when inTargetDocument:
                        aliases.Add(new AliasReference
                        {
                            Name = alias.Value.Value,
                            Sequence = sequence++,
                            Start = alias.Start,
                            End = alias.End,
                            OriginalBinding = currentDefinitions.TryGetValue(alias.Value.Value, out var binding) ? binding : -1,
                        });
                        break;

                    case NodeEvent nodeEvent when inTargetDocument && !nodeEvent.Anchor.IsEmpty:
                        var definitionId = definitions.Count;
                        definitions.Add(new AnchorDefinition
                        {
                            Name = nodeEvent.Anchor.Value,
                            Sequence = sequence++,
                            EventStart = nodeEvent.Start,
                        });
                        currentDefinitions[nodeEvent.Anchor.Value] = definitionId;
                        if (nodeEvent.Anchor.Value == oldName)
                        {
                            oldNameSeen++;
                            if (oldNameSeen == occurrence)
                            {
                                targetDefinitionId = definitionId;
                            }
                        }
                        break;
                }
            }

            documentCount = documentIndex + 1;
        }

        private static List<AnchorRenameConflict> FindBindingConflicts(
            List<AnchorDefinition> definitions,
            List<AliasReference> aliases,
            int targetDefinitionId,
            string newName)
        {
            var conflicts = new List<AnchorRenameConflict>();

            foreach (var alias in aliases)
            {
                var renamedAliasName = alias.OriginalBinding == targetDefinitionId ? newName : alias.Name;
                var newBinding = ResolveBinding(definitions, alias.Sequence, targetDefinitionId, newName, renamedAliasName);

                if (newBinding != alias.OriginalBinding)
                {
                    string message;
                    if (alias.OriginalBinding == targetDefinitionId)
                    {
                        message = $"The alias would no longer bind to the renamed anchor; it would be captured by a later definition of '&{newName}'.";
                    }
                    else if (alias.OriginalBinding >= 0)
                    {
                        message = "The alias is currently bound to a different anchor definition; the rename would rebind it.";
                    }
                    else
                    {
                        message = "The alias is currently unbound; the rename would bind it to the renamed anchor.";
                    }

                    conflicts.Add(new AnchorRenameConflict(alias.Start, alias.End, alias.Name, message));
                }
            }

            return conflicts;
        }

        private static int ResolveBinding(
            List<AnchorDefinition> definitions,
            int aliasSequence,
            int targetDefinitionId,
            string newName,
            string aliasName)
        {
            var binding = -1;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition.Sequence >= aliasSequence)
                {
                    break;
                }

                var definitionName = i == targetDefinitionId ? newName : definition.Name;
                if (definitionName == aliasName)
                {
                    binding = i;
                }
            }

            return binding;
        }

        private static string ApplyRename(
            string yaml,
            List<AnchorDefinition> definitions,
            List<AliasReference> aliases,
            int targetDefinitionId,
            string oldName,
            string newName)
        {
            var edits = new List<TextEdit>();

            var targetDefinition = definitions[targetDefinitionId];
            var anchorTokenStart = LocateAnchorToken(yaml, targetDefinition.EventStart, oldName);
            edits.Add(new TextEdit(anchorTokenStart, 1 + oldName.Length, "&" + newName));

            foreach (var alias in aliases)
            {
                if (alias.OriginalBinding == targetDefinitionId)
                {
                    var start = checked((int)alias.Start.Index);
                    var length = checked((int)(alias.End.Index - alias.Start.Index));
                    edits.Add(new TextEdit(start, length, "*" + newName));
                }
            }

            edits.Sort((left, right) => right.Start.CompareTo(left.Start));

            var builder = new StringBuilder(yaml);
            foreach (var edit in edits)
            {
                builder.Remove(edit.Start, edit.Length);
                builder.Insert(edit.Start, edit.Replacement);
            }

            return builder.ToString();
        }

        private static void VerifyReparses(string newText)
        {
            var parser = new Parser(new StringReader(newText));
            while (parser.MoveNext())
            {
            }
        }

        /// <summary>
        /// Locates the '&amp;name' anchor token of a node event. Node properties (tag and anchor,
        /// in any order) always appear at the very beginning of the event, so the anchor token is
        /// found by skipping an optional leading tag property.
        /// </summary>
        private static int LocateAnchorToken(string text, Mark eventStart, string anchorName)
        {
            var position = checked((int)eventStart.Index);

            // Skip a leading tag property, if present.
            if (position < text.Length && text[position] == '!')
            {
                if (position + 1 < text.Length && text[position + 1] == '<')
                {
                    var closing = text.IndexOf('>', position + 2);
                    if (closing < 0)
                    {
                        throw new InvalidOperationException("Could not locate the anchor token: unterminated verbatim tag.");
                    }

                    position = closing + 1;
                }
                else
                {
                    while (position < text.Length && !IsWhiteBreakOrZero(text[position]))
                    {
                        position++;
                    }
                }

                while (position < text.Length && text[position] != '\0' && IsWhiteBreakOrZero(text[position]))
                {
                    position++;
                }
            }

            if (position >= text.Length || text[position] != '&')
            {
                throw new InvalidOperationException($"Could not locate the anchor token '&{anchorName}' at line {eventStart.Line}, column {eventStart.Column}.");
            }

            var nameStart = position + 1;
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsAnchorNameCharacter(text[nameEnd]))
            {
                nameEnd++;
            }

            if (nameEnd - nameStart != anchorName.Length || string.Compare(text, nameStart, anchorName, 0, anchorName.Length, StringComparison.Ordinal) != 0)
            {
                throw new InvalidOperationException($"Could not locate the anchor token '&{anchorName}' at line {eventStart.Line}, column {eventStart.Column}.");
            }

            return position;
        }

        private static bool IsValidAnchorName(string name)
        {
            if (name.Length == 0)
            {
                return false;
            }

            foreach (var c in name)
            {
                if (!IsAnchorNameCharacter(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAnchorNameCharacter(char c) =>
            !IsWhiteBreakOrZero(c) && DisallowedAnchorCharacters.IndexOf(c) < 0;

        private static bool IsWhiteBreakOrZero(char c)
        {
            switch (c)
            {
                case '\0':
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '\u0085':
                case '\u2028':
                case '\u2029':
                    return true;
                default:
                    return false;
            }
        }

        private readonly struct TextEdit
        {
            public readonly int Start;
            public readonly int Length;
            public readonly string Replacement;

            public TextEdit(int start, int length, string replacement)
            {
                Start = start;
                Length = length;
                Replacement = replacement;
            }
        }
    }
}
