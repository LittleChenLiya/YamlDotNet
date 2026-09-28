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
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Test.RepresentationModel
{
    public class DeterministicAnchorTests
    {
        private static string SaveDeterministic(YamlStream stream)
        {
            var buffer = new StringWriter();
            stream.Save(buffer, AnchorNamingMode.Deterministic);
            return buffer.ToString().Replace("\r\n", "\n");
        }

        private static YamlStream Reload(string yaml)
        {
            var reloaded = new YamlStream();
            reloaded.Load(new StringReader(yaml));
            return reloaded;
        }

        /// <summary>
        /// Demonstration graph: two keys share the same sequence and a mapping references itself.
        /// </summary>
        private static YamlMappingNode BuildSharedAndCyclicGraph()
        {
            var sharedSequence = new YamlSequenceNode(new YamlScalarNode("item"));
            var root = new YamlMappingNode
            {
                { "first", sharedSequence },
                { "second", sharedSequence },
            };
            root.Add("self", root);
            return root;
        }

        [Fact]
        public void SharedNodesAndCyclesProduceDeterministicOutputAndSurviveReload()
        {
            var firstOutput = SaveDeterministic(new YamlStream(new YamlDocument(BuildSharedAndCyclicGraph())));
            var secondOutput = SaveDeterministic(new YamlStream(new YamlDocument(BuildSharedAndCyclicGraph())));

            firstOutput.Should().Be(secondOutput);

            const string expected =
                "&a1\n" +
                "first: &a2\n" +
                "- item\n" +
                "second: *a2\n" +
                "self: *a1\n" +
                "...\n";
            firstOutput.Should().Be(expected);

            var reloaded = (YamlMappingNode)Reload(firstOutput).Documents[0].RootNode;

            reloaded["first"].Should().BeSameAs(reloaded["second"]);
            reloaded["self"].Should().BeSameAs(reloaded);
        }

        [Fact]
        public void EqualButNotIdenticalNodesDoNotGetAliases()
        {
            var root = new YamlMappingNode
            {
                { "first", new YamlSequenceNode(new YamlScalarNode("value")) },
                { "second", new YamlSequenceNode(new YamlScalarNode("value")) },
            };

            var output = SaveDeterministic(new YamlStream(new YamlDocument(root)));

            output.Should().NotContain("&");
            output.Should().NotContain("*");

            var reloaded = (YamlMappingNode)Reload(output).Documents[0].RootNode;
            reloaded["first"].Should().NotBeSameAs(reloaded["second"]);
        }

        [Fact]
        public void ExplicitAnchorNameConflictsWithGeneratedName()
        {
            var sharedSequence = new YamlSequenceNode(new YamlScalarNode("item"));
            var root = new YamlMappingNode
            {
                { "x", new YamlScalarNode("unrelated") },
                { "first", sharedSequence },
                { "second", sharedSequence },
            };
            ((YamlScalarNode)root["x"]).Anchor = new AnchorName("a1");

            var stream = new YamlStream(new YamlDocument(root));

            Action save = () => SaveDeterministic(stream);

            save.Should().Throw<YamlException>()
                .WithMessage("*conflicts with an existing explicit anchor*");
        }

        [Fact]
        public void ExplicitAnchorNamesArePreservedAndDuplicateNamesAreRejected()
        {
            var sharedSequence = new YamlSequenceNode(new YamlScalarNode("item"))
            {
                Anchor = new AnchorName("explicit"),
            };
            var root = new YamlMappingNode
            {
                { "first", sharedSequence },
                { "second", sharedSequence },
            };

            var output = SaveDeterministic(new YamlStream(new YamlDocument(root)));

            output.Should().Contain("first: &explicit");
            output.Should().Contain("second: *explicit");

            var duplicateFirst = new YamlScalarNode("one") { Anchor = new AnchorName("dup") };
            var duplicateSecond = new YamlScalarNode("two") { Anchor = new AnchorName("dup") };
            var duplicateRoot = new YamlSequenceNode(duplicateFirst, duplicateSecond);
            var duplicateStream = new YamlStream(new YamlDocument(duplicateRoot));

            Action save = () => SaveDeterministic(duplicateStream);

            save.Should().Throw<YamlException>()
                .WithMessage("*assigned to more than one node*");
        }

        [Fact]
        public void MultipleDocumentsHaveIndependentAnchorScopes()
        {
            YamlNode BuildDocument()
            {
                var shared = new YamlSequenceNode(new YamlScalarNode("item"));
                return new YamlMappingNode
                {
                    { "first", shared },
                    { "second", shared },
                };
            }

            var stream = new YamlStream(
                new YamlDocument(BuildDocument()),
                new YamlDocument(BuildDocument()));

            var output = SaveDeterministic(stream);

            output.Should().Contain("&a1");
            output.Should().Contain("*a1");
            output.Where(character => character == '&').Should().HaveCount(2);

            var reloaded = Reload(output);
            reloaded.Documents.Should().HaveCount(2);

            foreach (var document in reloaded.Documents)
            {
                var mapping = (YamlMappingNode)document.RootNode;
                mapping["first"].Should().BeSameAs(mapping["second"]);
            }
        }

        [Fact]
        public void EmptyStreamSavesDeterministically()
        {
            var output = SaveDeterministic(new YamlStream());

            output.Should().BeEmpty();

            var reloaded = Reload(output);
            reloaded.Documents.Should().BeEmpty();
        }

        [Fact]
        public void ChangingMappingEntryOrderChangesTraversalOrderButKeepsNamingStable()
        {
            YamlStream BuildWithOrder(bool sharedFirst)
            {
                var shared = new YamlSequenceNode(new YamlScalarNode("item"));
                var root = sharedFirst
                    ? new YamlMappingNode
                    {
                        { "shared", shared },
                        { "other", new YamlScalarNode("plain") },
                    }
                    : new YamlMappingNode
                    {
                        { "other", new YamlScalarNode("plain") },
                        { "shared", shared },
                    };

                root.Add("again", shared);
                return new YamlStream(new YamlDocument(root));
            }

            var firstOrder = SaveDeterministic(BuildWithOrder(true));
            const string expectedFirst =
                "shared: &a1\n" +
                "- item\n" +
                "other: plain\n" +
                "again: *a1\n" +
                "...\n";
            firstOrder.Should().Be(expectedFirst);

            var secondOrder = SaveDeterministic(BuildWithOrder(false));
            const string expectedSecond =
                "other: plain\n" +
                "shared: &a1\n" +
                "- item\n" +
                "again: *a1\n" +
                "...\n";
            secondOrder.Should().Be(expectedSecond);
        }

        [Fact]
        public void DeterministicSaveDoesNotMutateCallerNodesAndIsRepeatable()
        {
            var graph = BuildSharedAndCyclicGraph();
            var sharedSequence = graph["first"];

            graph.Anchor.IsEmpty.Should().BeTrue();
            sharedSequence.Anchor.IsEmpty.Should().BeTrue();

            var first = SaveDeterministic(new YamlStream(new YamlDocument(graph)));
            var second = SaveDeterministic(new YamlStream(new YamlDocument(graph)));

            first.Should().Be(second);
            graph.Anchor.IsEmpty.Should().BeTrue();
            sharedSequence.Anchor.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void DefaultSaveBehaviorIsUnchanged()
        {
            var shared = new YamlSequenceNode(new YamlScalarNode("item"));
            var root = new YamlMappingNode
            {
                { "first", shared },
                { "second", shared },
            };
            var stream = new YamlStream(new YamlDocument(root));

            var buffer = new StringWriter();
            stream.Save(buffer);
            var output = buffer.ToString();

            output.Should().StartWith("first: &");
            output.Should().Contain("\nsecond: *");
            shared.Anchor.IsEmpty.Should().BeFalse("the default mode assigns anchors on the nodes themselves");
        }

        [Fact]
        public void ForwardAliasIsRejectedBeforeWriting()
        {
            var sequence = new YamlSequenceNode
            {
                new YamlAliasNode(new AnchorName("later")),
                new YamlScalarNode("value") { Anchor = new AnchorName("later") },
            };
            var stream = new YamlStream(new YamlDocument(sequence));

            Action save = () => SaveDeterministic(stream);

            save.Should().Throw<YamlException>()
                .WithMessage("*forward aliases cannot be emitted*");
        }

        [Fact]
        public void DanglingAliasIsRejectedBeforeWriting()
        {
            var sequence = new YamlSequenceNode
            {
                new YamlAliasNode(new AnchorName("missing")),
            };
            var stream = new YamlStream(new YamlDocument(sequence));

            Action save = () => SaveDeterministic(stream);

            save.Should().Throw<YamlException>()
                .WithMessage("*does not reference a node that has an anchor*");
        }

        [Fact]
        public void AliasOnlyDocumentIsRejectedBeforeWriting()
        {
            var stream = new YamlStream(new YamlDocument(new YamlAliasNode(new AnchorName("root"))));

            Action save = () => SaveDeterministic(stream);

            save.Should().Throw<YamlException>()
                .WithMessage("*cannot contain only an alias*");
        }

        [Fact]
        public void SelfReferencingMappingKeyGetsAnAnchor()
        {
            var root = new YamlMappingNode();
            root.Add(root, new YamlScalarNode("value"));

            var stream = new YamlStream(new YamlDocument(root));
            var output = SaveDeterministic(stream);
            var secondOutput = SaveDeterministic(stream);

            output.Should().Contain("&a1");
            output.Should().Contain("*a1");
            output.Should().Be(secondOutput);
            root.Anchor.IsEmpty.Should().BeTrue();
        }
    }
}
