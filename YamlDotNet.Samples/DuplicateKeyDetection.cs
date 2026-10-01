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
    public class DuplicateKeyDetection
    {
        private readonly ITestOutputHelper output;

        public DuplicateKeyDetection(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Sample(
            DisplayName = "Detecting duplicate mapping keys",
            Description = "Shows how to wrap a parser with DuplicateKeyCheckingParser to report duplicate and unsupported keys."
        )]
        public void Main()
        {
            // Wrap any IParser with the duplicate-key checker. Scalar keys are resolved
            // according to the YAML core schema before being compared; pass
            // ScalarKeyNormalizers.String (or a custom IScalarKeyNormalizer) to compare
            // the raw strings instead.
            var parser = new DuplicateKeyCheckingParser(
                new Parser(new StringReader(Document)),
                ScalarKeyNormalizers.CoreSchema);

            // The checker observes the event stream as it is consumed; the events
            // themselves are passed through unmodified.
            while (parser.MoveNext())
            {
            }

            foreach (var diagnostic in parser.Diagnostics)
            {
                if (diagnostic.Kind == DuplicateKeyDiagnosticKind.DuplicateKey)
                {
                    output.WriteLine(
                        "Duplicate key at {0} (first occurrence at {1})",
                        diagnostic.KeyStart,
                        diagnostic.FirstKeyStart);
                }
                else
                {
                    output.WriteLine(
                        "Unsupported key ({0}) at {1}: {2}",
                        diagnostic.Kind,
                        diagnostic.KeyStart,
                        diagnostic.Message);
                }
            }
        }

        private const string Document = @"---
receipt: Oz-Ware Purchase Invoice
receipt: Duplicate receipt
quantity: 4
quantity: 04
? {complex: key}
: unsupported
";
    }
}
