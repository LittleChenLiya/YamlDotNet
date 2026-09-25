# Duplicate key detection

YamlDotNet can check a YAML event stream for duplicate mapping keys through the
`DuplicateKeyDetectorParser` class, a composable `IParser` decorator located in the
`YamlDotNet.Core` namespace. The decorator forwards every event unchanged, so it can be
composed with any other parser (for example `Parser` or `MergingParser`):

```c#
using YamlDotNet.Core;

var parser = new DuplicateKeyDetectorParser(
    new Parser(new StringReader(yaml)),
    new DuplicateKeyDetectorOptions
    {
        KeyNormalizer = ScalarKeyNormalizers.CoreSchema,
        OnDiagnostic = d => Console.WriteLine(d),
    });

while (parser.MoveNext())
{
    // Consume events as usual.
}

foreach (var diagnostic in parser.Diagnostics)
{
    // DuplicateKeyDiagnosticKind.DuplicateKey diagnostics expose the start and end
    // marks of both the first occurrence (FirstKeyStart / FirstKeyEnd) and the
    // duplicate (KeyStart / KeyEnd).
}
```

The detector is opt-in: the plain `Parser` does not perform duplicate key detection and
does not impose any additional limits.

## How keys are compared

The caller chooses how scalar keys are compared by injecting an
`IScalarKeyNormalizer` through `DuplicateKeyDetectorOptions.KeyNormalizer`:

- `ScalarKeyNormalizers.Ordinal` (default) compares the exact string value of the
  scalar, so the keys `1` and `01` are different.
- `ScalarKeyNormalizers.CoreSchema` resolves plain scalars according to the YAML 1.2
  Core Schema (null, bool, int, float) before comparing them, so `1`, `01`, `0x1`
  and `0o1` are all equal. Numbers are parsed with the invariant culture, independently
  of the current culture. Scalars with a non-plain style or an explicit `!!str` tag are
  always treated as strings, so `!!str 1` is not a duplicate of the integer `1`.
- A custom `IScalarKeyNormalizer` implementation can be injected to apply any other
  comparison rules.

Collection keys (sequences and mappings used as keys) are compared structurally, and
alias keys are compared as the node they reference.

## Scoping

Each mapping gets its own scope: identical keys in different mappings — including
mappings inside the same sequence — or in different documents of the same stream do not
conflict. Anchors are also tracked per document.

## Unsupported keys and the node budget

Anchored nodes are canonicalized at most once and aliases reuse that canonical form, so
the detector never expands the whole alias graph. The amount of work is bounded by
`DuplicateKeyDetectorOptions.NodeBudget` (default 4096 nodes per document); when the
budget is exceeded, a `NodeBudgetExceeded` diagnostic is reported and detection is
disabled for the remainder of the document.

Keys whose equality cannot be determined — alias cycles, unresolved aliases, or keys
affected by the budget — are never silently ignored: they are reported as
`UnsupportedKey` diagnostics.

## Samples and tests

- A runnable sample that prints duplicate positions and unsupported keys is available in
  `YamlDotNet.Samples/DetectingDuplicateKeys.cs`.
- The behavior is covered by `YamlDotNet.Test/Core/DuplicateKeyDetectorParserTests.cs`,
  which includes event fixtures built with `Yaml.ParserForText`.

