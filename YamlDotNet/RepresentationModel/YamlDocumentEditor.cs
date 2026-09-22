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
    /// Provides source-level editing operations over YAML documents.
    /// The operations are driven by the parser events and the representation model,
    /// but produce their result by rewriting only the relevant tokens of the
    /// original text, so formatting, comments and unrelated content are preserved
    /// byte-for-byte and aliases are never expanded.
    /// </summary>
    public static class YamlDocumentEditor
    {
        /// <summary>
        /// Renames one anchor definition and every alias that is bound to it,
        /// without touching any other byte of the source text.
        /// </summary>
        /// <param name="yaml">The complete YAML source text.</param>
        /// <param name="documentIndex">The zero-based index of the document that contains the anchor.</param>
        /// <param name="oldName">The current name of the anchor (without the '&amp;' prefix).</param>
        /// <param name="newName">The new name of the anchor (without the '&amp;' prefix).</param>
        /// <param name="occurrence">
        /// The one-based occurrence of the definition of <paramref name="oldName"/> inside the document,
        /// because a document may redefine the same anchor name several times.
        /// </param>
        /// <returns>The full source text with only the target anchor token and its bound alias tokens replaced.</returns>
        /// <remarks>
        /// An alias is bound to the closest preceding definition of the same name within the same document;
        /// documents have independent anchor scopes. Only the definition selected by
        /// <paramref name="occurrence"/> and the aliases bound to it are renamed. If the new name would
        /// change the binding of any alias in the document (for example by capturing an existing
        /// <c>*newName</c> alias, or by having a renamed alias captured by a later
        /// <c>&amp;newName</c> definition), the whole operation is rejected with an
        /// <see cref="AnchorRenameConflictException"/> that reports the conflicting positions.
        /// If the anchor does not exist, a name is invalid, or the source has syntax errors,
        /// an exception is thrown and no partial text is returned.
        /// </remarks>
        public static string RenameAnchor(string yaml, int documentIndex, string oldName, string newName, int occurrence = 1)
        {
            if (yaml == null)
            {
                throw new ArgumentNullException(nameof(yaml));
            }

            if (documentIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentIndex), documentIndex, "The document index must be greater than or equal to zero.");
            }

            if (occurrence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(occurrence), occurrence, "The occurrence must be greater than or equal to one.");
            }

            ValidateAnchorName(oldName, nameof(oldName));
            ValidateAnchorName(newName, nameof(newName));

            var documents = ParseDocuments(yaml);

            if (documentIndex >= documents.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(documentIndex), documentIndex, $"The source contains only {documents.Count} document(s).");
            }

            var document = documents[documentIndex];

            var targetIndex = -1;
            var seen = 0;
            for (var i = 0; i < document.Entries.Count; i++)
            {
                var entry = document.Entries[i];
                if (entry.IsDefinition && entry.Name == oldName && ++seen == occurrence)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                throw new AnchorNotFoundException(
                    $"The anchor '&{oldName}' does not have occurrence #{occurrence} in document {documentIndex}.");
            }

            var bindingsBefore = ComputeBindings(document.Entries);

            // Simulate the rename: the target definition and every alias bound to it take the new name.
            List<AnchorEntry> renamedEntries = [.. document.Entries];
            for (var i = 0; i < renamedEntries.Count; i++)
            {
                if (i == targetIndex)
                {
                    renamedEntries[i] = renamedEntries[i].WithName(newName);
                }
                else if (!renamedEntries[i].IsDefinition && bindingsBefore[i] == targetIndex)
                {
                    renamedEntries[i] = renamedEntries[i].WithName(newName);
                }
            }

            var bindingsAfter = ComputeBindings(renamedEntries);

            List<Mark>? conflicts = null;
            for (var i = 0; i < document.Entries.Count; i++)
            {
                if (!document.Entries[i].IsDefinition && bindingsBefore[i] != bindingsAfter[i])
                {
                    conflicts ??= [];
                    conflicts.Add(document.Entries[i].Start);
                }
            }

            if (conflicts != null)
            {
                throw new AnchorRenameConflictException(
                    $"Renaming '&{oldName}' to '&{newName}' in document {documentIndex} would change the binding of {conflicts.Count} alias(es).",
                    conflicts);
            }

            var target = document.Entries[targetIndex];
            var anchorNameStart = LocateAnchorNameStart(yaml, target.Start, oldName);

            List<TextEdit> edits = [new TextEdit(anchorNameStart, oldName.Length, newName)];

            for (var i = 0; i < document.Entries.Count; i++)
            {
                var entry = document.Entries[i];
                if (!entry.IsDefinition && bindingsBefore[i] == targetIndex)
                {
                    var aliasNameStart = (int)entry.Start.Index + 1;
                    if (aliasNameStart + oldName.Length > yaml.Length ||
                        string.CompareOrdinal(yaml, aliasNameStart, oldName, 0, oldName.Length) != 0)
                    {
                        throw new InvalidOperationException($"Could not locate the alias '*{oldName}' at {entry.Start} in the source text.");
                    }

                    edits.Add(new TextEdit(aliasNameStart, oldName.Length, newName));
                }
            }

            var result = ApplyEdits(yaml, edits);

            VerifyResult(result, documents, documentIndex, renamedEntries);

            return result;
        }

        private static void ValidateAnchorName(string name, string paramName)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("The anchor name must not be null or empty.", paramName);
            }

            foreach (var c in name)
            {
                if (IsWhiteOrBreak(c) || c is '[' or ']' or '{' or '}' or ',')
                {
                    throw new ArgumentException($"The anchor name '{name}' is not a valid YAML anchor name.", paramName);
                }
            }
        }

        private static List<DocumentAnchors> ParseDocuments(string yaml)
        {
            var parser = new Parser(new StringReader(yaml));
            List<DocumentAnchors> documents = [];
            DocumentAnchors? current = null;

            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case DocumentStart:
                        current = new DocumentAnchors();
                        documents.Add(current);
                        break;

                    case AnchorAlias alias:
                        current?.Entries.Add(AnchorEntry.Alias(alias.Value.Value, alias.Start, alias.End));
                        break;

                    case NodeEvent nodeEvent when !nodeEvent.Anchor.IsEmpty:
                        current?.Entries.Add(AnchorEntry.Definition(nodeEvent.Anchor.Value, nodeEvent.Start));
                        break;
                }
            }

            return documents;
        }

        private static int[] ComputeBindings(IReadOnlyList<AnchorEntry> entries)
        {
            var bindings = new int[entries.Count];
            var lastDefinitionByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.IsDefinition)
                {
                    lastDefinitionByName[entry.Name] = i;
                    bindings[i] = -1;
                }
                else
                {
                    bindings[i] = lastDefinitionByName.TryGetValue(entry.Name, out var definition) ? definition : -1;
                }
            }

            return bindings;
        }

        private static int LocateAnchorNameStart(string yaml, Mark nodeStart, string name)
        {
            var i = (int)nodeStart.Index;
            // The anchor and the tag can appear in any order, so scan at most two property tokens.
            for (var property = 0; property < 2; property++)
            {
                SkipSeparation(yaml, ref i);
                if (i >= yaml.Length)
                {
                    break;
                }

                if (yaml[i] == '!')
                {
                    SkipTag(yaml, ref i);
                    continue;
                }

                if (yaml[i] == '&')
                {
                    var nameStart = i + 1;
                    if (nameStart + name.Length <= yaml.Length &&
                        string.CompareOrdinal(yaml, nameStart, name, 0, name.Length) == 0 &&
                        (nameStart + name.Length == yaml.Length || IsAnchorTerminator(yaml[nameStart + name.Length])))
                    {
                        return nameStart;
                    }
                }

                break;
            }

            throw new InvalidOperationException($"Could not locate the anchor '&{name}' at {nodeStart} in the source text.");
        }

        private static void SkipSeparation(string yaml, ref int i)
        {
            while (i < yaml.Length)
            {
                var c = yaml[i];
                if (IsWhiteOrBreak(c))
                {
                    i++;
                }
                else if (c == '#')
                {
                    while (i < yaml.Length && !IsBreak(yaml[i]))
                    {
                        i++;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private static void SkipTag(string yaml, ref int i)
        {
            i++; // Eat '!'
            if (i < yaml.Length && yaml[i] == '<')
            {
                // Verbatim tag: !<...>
                while (i < yaml.Length && yaml[i] != '>')
                {
                    i++;
                }

                if (i < yaml.Length)
                {
                    i++; // Eat '>'
                }
            }
            else
            {
                while (i < yaml.Length && !IsWhiteOrBreak(yaml[i]) && yaml[i] is not ('[' or ']' or '{' or '}' or ','))
                {
                    i++;
                }
            }
        }

        private static bool IsWhiteOrBreak(char c) => c is ' ' or '\t' || IsBreak(c);

        private static bool IsBreak(char c) => c is '\r' or '\n' or '\x85' or '\u2028' or '\u2029';

        private static bool IsAnchorTerminator(char c) => IsWhiteOrBreak(c) || c is '[' or ']' or '{' or '}' or ',';

        private static string ApplyEdits(string yaml, List<TextEdit> edits)
        {
            edits.Sort((left, right) => right.Start.CompareTo(left.Start));
            var builder = new StringBuilder(yaml);
            foreach (var edit in edits)
            {
                builder.Remove(edit.Start, edit.Length);
                builder.Insert(edit.Start, edit.Replacement);
            }

            return builder.ToString();
        }

        private static void VerifyResult(string result, List<DocumentAnchors> documentsBefore, int documentIndex, List<AnchorEntry> expectedEntries)
        {
            var documentsAfter = ParseDocuments(result);
            if (documentsAfter.Count != documentsBefore.Count)
            {
                throw new InvalidOperationException("The rename produced invalid YAML: the document count changed.");
            }

            for (var d = 0; d < documentsAfter.Count; d++)
            {
                var expected = d == documentIndex ? expectedEntries : documentsBefore[d].Entries;
                var actual = documentsAfter[d].Entries;
                if (actual.Count != expected.Count)
                {
                    throw new InvalidOperationException("The rename produced invalid YAML: the anchor structure changed.");
                }

                for (var i = 0; i < actual.Count; i++)
                {
                    if (actual[i].IsDefinition != expected[i].IsDefinition || actual[i].Name != expected[i].Name)
                    {
                        throw new InvalidOperationException("The rename produced invalid YAML: the anchor structure changed.");
                    }
                }
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

        private readonly struct AnchorEntry
        {
            public readonly bool IsDefinition;
            public readonly string Name;
            public readonly Mark Start;
            public readonly Mark End;

            private AnchorEntry(bool isDefinition, string name, Mark start, Mark end)
            {
                IsDefinition = isDefinition;
                Name = name;
                Start = start;
                End = end;
            }

            public static AnchorEntry Definition(string name, Mark start) => new AnchorEntry(true, name, start, start);

            public static AnchorEntry Alias(string name, Mark start, Mark end) => new AnchorEntry(false, name, start, end);

            public AnchorEntry WithName(string name) => new AnchorEntry(IsDefinition, name, Start, End);
        }

        private sealed class DocumentAnchors
        {
            public List<AnchorEntry> Entries { get; } = [];
        }
    }
}
