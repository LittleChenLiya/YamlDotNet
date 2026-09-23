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
using System.Text;
using Xunit.Abstractions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Samples.Helpers;

namespace YamlDotNet.Samples
{
    public class ExpandingMergeKeysWithOriginTracking
    {
        private readonly ITestOutputHelper output;

        public ExpandingMergeKeysWithOriginTracking(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Expanding merge keys with origin tracking",
            Description = "Shows how YamlMergeExpander expands '<<' merge keys in the representation model " +
                "and reports where each resulting value was defined. Unlike MergingParser, which rewrites " +
                "the event stream during parsing and discards provenance, YamlMergeExpander is an opt-in, " +
                "post-parsing step that leaves the input tree unchanged and records the origin of every value."
        )]
        public void Main()
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(Document));

            var result = YamlMergeExpander.Expand(yaml.Documents[0]);

            output.WriteLine("Expanded values and their origins:");
            PrintNode(result, result.Root, "$", 0);

            output.WriteLine(string.Empty);
            output.WriteLine("Relationship to MergingParser:");
            output.WriteLine("  MergingParser merges '<<' keys at the event-stream level while parsing;");
            output.WriteLine("  it is transparent to deserializers but keeps no trace of where values came from.");
            output.WriteLine("  YamlMergeExpander runs on the parsed representation model, on demand, and");
            output.WriteLine("  produces an independent tree plus the origin (definition, anchor chain, use");
            output.WriteLine("  site) of every resulting value. Plain parsing entry points are unaffected.");
        }

        private void PrintNode(YamlMergeExpansionResult result, YamlNode node, string path, int depth)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var entry in mapping.Children)
                    {
                        var key = ((YamlScalarNode)entry.Key).Value;
                        PrintNode(result, entry.Value, path + "." + key, depth + 1);
                    }

                    break;

                case YamlSequenceNode sequence:
                    for (var i = 0; i < sequence.Children.Count; i++)
                    {
                        PrintNode(result, sequence.Children[i], path + "[" + i + "]", depth + 1);
                    }

                    break;

                case YamlScalarNode scalar:
                    var origin = result.GetOrigin(scalar);
                    output.WriteLine("{0} = {1}", path, scalar.Value);
                    if (origin != null)
                    {
                        var chain = origin.AnchorChain.Count == 0
                            ? "(local)"
                            : string.Join(" -> ", origin.AnchorChain);
                        output.WriteLine(
                            "    defined at line {0}, column {1}; anchors: {2}; used at line {3}, column {4}",
                            origin.DefinedAt.Line,
                            origin.DefinedAt.Column,
                            chain,
                            origin.UsedAt.Line,
                            origin.UsedAt.Column);
                    }

                    break;
            }
        }

        private const string Document = @"---
base: &base
  x: 1
  y: 2
next: &next
  x: 3
merged:
  <<: [*base, *next]
  y: 9
...";
    }
}
