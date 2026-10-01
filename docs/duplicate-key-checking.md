# Duplicate key checking

`YamlDotNet.Core.DuplicateKeyCheckingParser` is an `IParser` decorator that detects
duplicate mapping keys while the event stream is consumed. It does not change the events
in any way, so it composes freely with other decorators such as `MergingParser`:

```csharp
var parser = new DuplicateKeyCheckingParser(
    new Parser(new StringReader(yaml)),
    ScalarKeyNormalizers.CoreSchema);

while (parser.MoveNext()) { /* consume events as usual */ }

foreach (var diagnostic in parser.Diagnostics)
{
    Console.WriteLine(diagnostic);
}
```

## Key comparison

The caller chooses how scalar keys are compared by passing an `IScalarKeyNormalizer`:

- `ScalarKeyNormalizers.Ordinal` compares the raw scalar text with ordinal string
  comparison. `1` and `01` are different keys.
- `ScalarKeyNormalizers.CoreSchema` (the default) resolves plain scalars according to the
  YAML 1.2 Core Schema (null, bool, int, float) before comparing, so `1`, `01`, `0x1`
  and `1e0`-style numerics that denote the same value are duplicates, while an explicitly
  string-tagged `!!str 1` is different from the integer `1`. All number parsing is
  culture-invariant.
- A custom `IScalarKeyNormalizer` can be injected for any other policy (for example
  case-insensitive keys).

Sequence keys are compared structurally. Alias keys are compared by the node they refer
to, so `&k key` followed by `*k` is a duplicate.

## Scoping

Every mapping gets its own scope: identical keys in different mappings (including the
items of a sequence), in nested mappings, or in different documents of the same stream do
not conflict.

## Unsupported keys

Keys that cannot be compared for equality are never silently ignored; they produce a
diagnostic whose `Kind` is one of:

- `UnsupportedMappingKey` — a mapping is used as (or inside) a key.
- `UnsupportedAliasCycle` — an alias refers to the anchor that is currently being defined.
- `UnsupportedUnknownAnchor` — an alias refers to an anchor that was never defined.
- `UnsupportedNodeBudgetExceeded` — the key contains more nodes than the configured budget.

## Node budget

The checker never expands the alias graph. Each key (and each anchored node, so that
aliases can later be resolved) is captured at most once, and a single key or anchored node
may contain at most `maxKeyNodes` nodes (default
`DuplicateKeyCheckingParser.DefaultMaxKeyNodes`, 1024). The wrapped `Parser` keeps its
own behavior and is not given any additional limits.

## Diagnostics

Every `DuplicateKeyDiagnostic` reports the start and end `Mark` of the offending key
(`KeyStart`/`KeyEnd`). For `DuplicateKey` diagnostics, `FirstKeyStart`/`FirstKeyEnd`
locate the first occurrence of the key in the same mapping. Diagnostics are accumulated in
`Diagnostics` and are also raised through the optional constructor callback as soon as
they are detected.

See `YamlDotNet.Samples/DuplicateKeyDetection.cs` for a runnable example that prints
duplicate positions and unsupported keys.
