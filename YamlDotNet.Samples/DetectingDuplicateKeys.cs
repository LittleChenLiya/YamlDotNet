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
using YamlDotNet.Samples.Helpers;

namespace YamlDotNet.Samples
{
    public class DetectingDuplicateKeys
    {
        private readonly ITestOutputHelper output;

        public DetectingDuplicateKeys(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Detecting duplicate keys",
            Description = "Shows how to check a YAML event stream for duplicate mapping keys."
        )]
        public void Main()
        {
            var input = new StringReader(Document);

            // Wrap any IParser with the duplicate key detector. Keys are compared after
            // resolving plain scalars according to the YAML 1.2 Core Schema, so the
            // keys "1" and "01" below are considered equal. Diagnostics can be printed
            // as they are detected through the callback, and are also collected in
            // the Diagnostics property.
            var parser = new DuplicateKeyDetectorParser(
                new Parser(input),
                new DuplicateKeyDetectorOptions
                {
                    KeyNormalizer = ScalarKeyNormalizers.CoreSchema,
                    OnDiagnostic = d => output.WriteLine(d.ToString()),
                });

            // The detector is a composable IParser decorator: consume the events as usual.
            while (parser.MoveNext())
            {
            }

            foreach (var diagnostic in parser.Diagnostics)
            {
                switch (diagnostic.Kind)
                {
                    case DuplicateKeyDiagnosticKind.DuplicateKey:
                        output.WriteLine(
                            "Duplicate key at {0}; first occurrence at {1}.",
                            diagnostic.KeyStart,
                            diagnostic.FirstKeyStart);
                        break;
                    case DuplicateKeyDiagnosticKind.UnsupportedKey:
                        output.WriteLine(
                            "Key at {0} cannot be compared: {1}",
                            diagnostic.KeyStart,
                            diagnostic.Message);
                        break;
                    case DuplicateKeyDiagnosticKind.NodeBudgetExceeded:
                        output.WriteLine(diagnostic.Message);
                        break;
                }
            }
        }

        private const string Document = @"---
receipt: Oz-Ware Purchase Invoice
1: first number key
01: duplicate of the numeric key 1
customer:
    given: Dorothy
    given: Gale
? &a [*a]
: unsupported key
...";
    }
}

