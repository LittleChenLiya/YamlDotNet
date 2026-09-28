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

using System.IO;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Samples.Helpers;

namespace YamlDotNet.Samples
{
    public class SavingDeterministicAnchors
    {
        private readonly ITestOutputHelper output;

        public SavingDeterministicAnchors(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Saving shared nodes with deterministic anchors",
            Description = "Saves a representation model with shared and cyclic nodes using stable a1, a2 anchors."
        )]
        public void Main()
        {
            // Two keys share the same sequence instance, and the mapping references itself.
            var sharedSequence = new YamlSequenceNode(new YamlScalarNode("item"));
            var root = new YamlMappingNode
            {
                { "first", sharedSequence },
                { "second", sharedSequence },
            };
            root.Add("self", root);

            // Saving twice with deterministic anchors produces identical bytes.
            string Save()
            {
                var buffer = new StringWriter();
                var stream = new YamlStream(new YamlDocument(root));
                stream.Save(buffer, AnchorNamingMode.Deterministic);
                return buffer.ToString();
            }

            var first = Save();
            var second = Save();

            output.WriteLine(first);
            Assert.Equal(first, second);

            // Reloading keeps the sharing relationships: first and second point at the same node,
            // and self points back at the root mapping.
            var reloaded = new YamlStream();
            reloaded.Load(new StringReader(first));

            var mapping = (YamlMappingNode)reloaded.Documents[0].RootNode;
            Assert.Same(mapping["first"], mapping["second"]);
            Assert.Same(mapping, mapping["self"]);

            output.WriteLine("first and second are identical: {0}", ReferenceEquals(mapping["first"], mapping["second"]));
            output.WriteLine("self is the root mapping: {0}", ReferenceEquals(mapping, mapping["self"]));
        }
    }
}
