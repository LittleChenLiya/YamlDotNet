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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace YamlDotNet.Tools.StringStyleSelectorCli
{
    /// <summary>
    /// A local command that demonstrates the <see cref="StringStyleSelector"/>.
    /// Given a string value, it prints the selected scalar style and the reason, and can
    /// optionally emit the value as YAML and re-parse it to verify the round-trip.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
            {
                Console.WriteLine("Usage: string-style-selector <value> [--emit] [--crlf]");
                Console.WriteLine();
                Console.WriteLine("Selects a safe scalar style for the given string value and prints the style and reason.");
                Console.WriteLine("  --emit   Emit the value as YAML and re-parse it to verify the round-trip.");
                Console.WriteLine("  --crlf   Use CRLF line endings when emitting.");
                return 0;
            }

            var value = args[0];
            var emit = Array.IndexOf(args, "--emit") >= 0;
            var crlf = Array.IndexOf(args, "--crlf") >= 0;

            var selection = StringStyleSelector.Select(value, TagName.Empty);
            Console.WriteLine($"Value:    {Escape(value)}");
            Console.WriteLine($"Style:    {selection.Style}");
            Console.WriteLine($"Reason:   {selection.Reason}");

            if (emit)
            {
                var newLine = crlf ? "\r\n" : "\n";
                var yaml = Emit(value, newLine);
                Console.WriteLine();
                Console.WriteLine("Emitted YAML:");
                Console.WriteLine(yaml);

                var deserialized = new DeserializerBuilder()
                    .WithAttemptingUnquotedStringTypeDeserialization()
                    .Build()
                    .Deserialize(yaml);

                var ok = deserialized is string s && s == value;
                Console.WriteLine($"Round-trip: {(ok ? "OK" : "FAILED")}");
            }

            return 0;
        }

        private static string Emit(string value, string newLine)
        {
            var settings = EmitterSettings.Default
                .WithStringStyleSelector()
                .WithNewLine(newLine);

            var writer = new StringWriter { NewLine = newLine };
            var emitter = new Emitter(writer, settings);
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart());
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.Any, true, true));
            emitter.Emit(new DocumentEnd(true));
            emitter.Emit(new StreamEnd());
            return writer.ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
