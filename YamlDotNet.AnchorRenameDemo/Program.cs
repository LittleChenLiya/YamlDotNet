using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

// Demo: rename an anchor in-place, printing the minimal diff, then show a
// rename that is rejected because the new name would capture another alias.

var yaml =
    "# comment mentioning &a and *a\n" +
    "other: &c 9\n" +
    "first: &a 1\n" +
    "mid: *a\n" +
    "keep: *c\n" +
    "second: &a 2\n" +
    "after: *a\n" +
    "quoted: \"*a is not an alias here\"\n" +
    "---\n" +
    "doc2: &a 3\n" +
    "ref2: *a\n";

Console.WriteLine("=== Original YAML ===");
Console.Write(yaml);

Console.WriteLine("=== Demo 1: rename first '&a' of document 0 to '&b' ===");
var renamed = YamlDocumentEditor.RenameAnchor(yaml, documentIndex: 0, oldName: "a", newName: "b", occurrence: 1);
PrintMinimalDiff(yaml, renamed);

Console.WriteLine();
Console.WriteLine("=== Demo 2: rename first '&a' of document 0 to '&c' (must be rejected) ===");
try
{
    YamlDocumentEditor.RenameAnchor(yaml, documentIndex: 0, oldName: "a", newName: "c", occurrence: 1);
    Console.WriteLine("ERROR: the rename should have been rejected!");
    return 1;
}
catch (AnchorRenameConflictException ex)
{
    Console.WriteLine($"Rejected: {ex.Message}");
    foreach (var position in ex.ConflictPositions)
    {
        Console.WriteLine($"  conflict at {position}");
    }
}

return 0;

static void PrintMinimalDiff(string before, string after)
{
    var beforeLines = SplitLines(before);
    var afterLines = SplitLines(after);
    var changes = 0;
    for (var i = 0; i < Math.Max(beforeLines.Count, afterLines.Count); i++)
    {
        var oldLine = i < beforeLines.Count ? beforeLines[i] : null;
        var newLine = i < afterLines.Count ? afterLines[i] : null;
        if (oldLine != newLine)
        {
            changes++;
            if (oldLine != null)
            {
                Console.WriteLine($"- {oldLine}");
            }

            if (newLine != null)
            {
                Console.WriteLine($"+ {newLine}");
            }
        }
    }

    Console.WriteLine($"({changes} line(s) changed; every other byte is identical)");
}

static List<string> SplitLines(string text)
{
    var lines = new List<string>();
    var start = 0;
    for (var i = 0; i < text.Length; i++)
    {
        if (text[i] == '\n')
        {
            lines.Add(text.Substring(start, i - start));
            start = i + 1;
        }
    }

    if (start < text.Length)
    {
        lines.Add(text.Substring(start));
    }

    return lines;
}

