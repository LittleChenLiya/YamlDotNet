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
using Xunit.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Samples.Helpers;

namespace YamlDotNet.Samples
{
    public class UseStringStyleSelector
    {
        private readonly ITestOutputHelper output;

        public UseStringStyleSelector(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Selecting string scalar styles explicitly",
            Description = "Shows how to choose plain, quoted or literal styles so that strings " +
                "such as 'yes' or '012' round-trip unchanged, and why each style was chosen."
        )]
        public void Main()
        {
            var values = new[]
            {
                "yes",                          // would implicitly resolve as a boolean
                "012",                          // would implicitly resolve as an (octal) integer
                "line one\nline two\n\n",       // two trailing line feeds: needs keep chomping
                "  indented first\nsecond line" // indented first line: needs an indentation indicator
            };

            var settings = EmitterSettings.Default
                .WithStringStyleSelector(v => StringStyleSelector.Select(v));

            foreach (var value in values)
            {
                // The rules can be called independently of the emitter...
                var decision = StringStyleSelector.Select(value);
                output.WriteLine($"Value:   {Escape(value)}");
                output.WriteLine($"Style:   {decision.Style}");
                output.WriteLine($"Reason:  {decision.Reason}");

                // ...or registered on the emitter so that they are applied automatically.
                var writer = new StringWriter();
                var emitter = new Emitter(writer, settings);
                emitter.Emit(new StreamStart());
                emitter.Emit(new DocumentStart());
                emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Any, true, true));
                emitter.Emit(new DocumentEnd(true));
                emitter.Emit(new StreamEnd());

                var yaml = writer.ToString();
                output.WriteLine("Emitted YAML:");
                output.WriteLine(yaml);

                // The emitted document re-parses to the exact same string.
                var parser = new Parser(new StringReader(yaml));
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                var roundTripped = parser.Consume<Scalar>().Value;
                output.WriteLine($"Round-trips verbatim: {roundTripped == value}");
                output.WriteLine();
            }
        }

        private static string Escape(string value)
        {
            return "'" + value.Replace("\n", "\n") + "'";
        }
    }
}
