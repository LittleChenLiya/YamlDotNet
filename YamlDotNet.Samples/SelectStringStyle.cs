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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Schemas;

namespace YamlDotNet.Samples
{
    /// <summary>
    /// Shows how to use the <see cref="StringStyleSelector"/> to choose a safe scalar style
    /// for string values, ensuring that the emitted YAML round-trips to the original string.
    /// </summary>
    public class SelectStringStyle
    {
        private readonly ITestOutputHelper output;

        public SelectStringStyle(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Selecting a safe string style",
            Description = "Shows how to choose a plain, quoted or literal style for a string value based on the target schema, and how to enable the selector on the emitter."
        )]
        public void Main()
        {
            // The selector is stateless and can be called independently. It returns the
            // recommended style together with the reason why that style was chosen.
            string[] values =
            {
                "yes",
                "012",
                "null",
                "hello",
                "a\nb\n",
                "a\nb\n\n",
                "  indented\nline2\n",
                "",
            };

            output.WriteLine("=== StringStyleSelector.Select(value, tag) ===");
            foreach (var value in values)
            {
                var selection = StringStyleSelector.Select(value, TagName.Empty);
                output.WriteLine($"  [{value.Replace("\n", "\\n")}] -> {selection.Style} ({selection.Reason})");
            }

            output.WriteLine();
            output.WriteLine("=== Round-trip through the emitter with the selector enabled ===");

            var settings = EmitterSettings.Default.WithStringStyleSelector();
            foreach (var value in values)
            {
                var yaml = Emit(value, settings, "\n");
                var deserialized = new DeserializerBuilder()
                    .WithAttemptingUnquotedStringTypeDeserialization()
                    .Build()
                    .Deserialize(yaml);

                var ok = deserialized is string s && s == value;
                output.WriteLine($"  [{value.Replace("\n", "\\n")}] -> round-trip: {ok}");
            }

            output.WriteLine();
            output.WriteLine("=== CRLF output preserves internal LF ===");

            var crlfSettings = EmitterSettings.Default
                .WithStringStyleSelector()
                .WithNewLine("\r\n");
            var crlfYaml = Emit("a\nb\n", crlfSettings, "\r\n");
            output.WriteLine(crlfYaml);

            // An explicit str tag prevents implicit resolution, so plain is safe even for
            // reserved words.
            output.WriteLine("=== Explicit str tag allows plain for reserved words ===");
            var explicitTag = StringStyleSelector.Select("yes", FailsafeSchema.Tags.Str);
            output.WriteLine($"  [yes] with str tag -> {explicitTag.Style} ({explicitTag.Reason})");
        }

        private static string Emit(string value, EmitterSettings settings, string newLine)
        {
            var writer = new StringWriter { NewLine = newLine };
            var emitter = new Emitter(writer, settings);
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart());
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Any, true, true));
            emitter.Emit(new DocumentEnd(true));
            emitter.Emit(new StreamEnd());
            return writer.ToString();
        }
    }
}
