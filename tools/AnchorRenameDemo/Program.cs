// Demo for YamlDotNet.RepresentationModel.YamlAnchorEditor.
// Run with: dotnet run --project tools/AnchorRenameDemo
// Prints the minimal diff produced by an anchor rename, and the conflict
// positions reported when a rename would change an alias binding.

using YamlDotNet.RepresentationModel;

var yaml =
    "first: &a 1\n" +
    "between: *a\n" +
    "second: &a 2\n" +
    "after: *a\n" +
    "# comment mentioning &a and *a\n" +
    "quoted: \"&a and *a stay\"\n" +
    "---\n" +
    "other: &a 9\n" +
    "ref: *a\n";

Console.WriteLine("=== Original YAML ===");
Console.Write(yaml);

Console.WriteLine("=== Demo 1: rename first '&a' of document 0 to '&renamed' ===");
var renamed = YamlAnchorEditor.RenameAnchor(yaml, documentIndex: 0, oldName: "a", newName: "renamed", occurrence: 1);
if (renamed.Success)
{
    PrintMinimalDiff(yaml, renamed.NewText!);
}
else
{
    Console.WriteLine($"Rejected: {renamed.ErrorMessage}");
}

Console.WriteLine("=== Demo 2: rename '&a' to '&b' where '&b' would capture other aliases ===");
var conflicting =
    "x: &a 1\n" +
    "y: &b 2\n" +
    "z: *b\n" +
    "w: *a\n";
Console.Write(conflicting);
var rejected = YamlAnchorEditor.RenameAnchor(conflicting, 0, "a", "b", 1);
Console.WriteLine($"Status: {rejected.Status}");
Console.WriteLine(rejected.ErrorMessage);
foreach (var conflict in rejected.Conflicts)
{
    Console.WriteLine($"  conflict: {conflict}");
}

static void PrintMinimalDiff(string oldText, string newText)
{
    var oldLines = SplitLines(oldText);
    var newLines = SplitLines(newText);
    var anyChange = false;
    var count = Math.Max(oldLines.Length, newLines.Length);
    for (var i = 0; i < count; i++)
    {
        var oldLine = i < oldLines.Length ? oldLines[i] : null;
        var newLine = i < newLines.Length ? newLines[i] : null;
        if (!string.Equals(oldLine, newLine, StringComparison.Ordinal))
        {
            anyChange = true;
            if (oldLine != null)
            {
                Console.WriteLine($"  - line {i + 1}: {oldLine}");
            }

            if (newLine != null)
            {
                Console.WriteLine($"  + line {i + 1}: {newLine}");
            }
        }
    }

    if (!anyChange)
    {
        Console.WriteLine("  (no changes)");
    }
}

static string[] SplitLines(string text) =>
    text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
