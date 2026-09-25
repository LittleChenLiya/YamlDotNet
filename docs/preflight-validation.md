# Deserialization preflight validation

YamlDotNet can optionally run a **preflight validation phase** before deserializing.
When enabled, the whole event stream is buffered (up to a configurable limit) and checked
against the target type and the registered tag mappings. Only when the complete stream is
known to contain no unpermitted runtime types does the deserializer start constructing
objects.

This guarantees that a rejected input has **zero side effects**: no constructor and no
property setter is executed, even if the offending tag or alias is located at the very
end of the input.

## Enabling preflight validation

```csharp
var deserializer = new DeserializerBuilder()
    .WithTagMapping("!cat", typeof(Cat))
    .WithTagMapping("!dog", typeof(Dog))
    .WithPreflightValidation(options =>
    {
        options.MaxBufferedEvents = 50_000; // optional, defaults to 100_000
        options.MaxDepth = 256;             // optional, defaults to 512
    })
    .Build();
```

- `WithPreflightValidation(...)` enables the phase; `WithoutPreflightValidation()` disables it again.
- `PreflightValidationOptions.MaxBufferedEvents` caps the total number of parsing events that
  may be buffered. Inputs exceeding the limit are rejected. Must be a positive integer.
- `PreflightValidationOptions.MaxDepth` caps the nesting depth of the input. Must be a positive integer.

After a successful validation the buffered events are **reused**: they are replayed through
the regular deserialization pipeline, so the input is not parsed twice.

## What is checked

- **Tags**: every tag must be registered through `WithTagMapping` (the default schema tags
  such as `!!str`, `!!int`, `!!map`, ... are pre-registered). The type a tag maps to must be
  assignable to the target type at the position where the tag appears, including collection
  element types and nullable positions.
- **Polymorphism**: positions whose target type is abstract or an interface require a tag
  (or a `WithTypeMapping` registration) that determines the concrete type. Ambiguous
  positions are rejected.
- **Aliases**: an alias must refer to a previously defined anchor, and the type of the
  anchored node must be compatible with the target type at **every** position where the
  alias is used. When the same anchor is referenced from an incompatible position, the
  error reports two marks: the alias use position (`Start`/`End`) and the anchor
  definition (`RelatedStart`/`RelatedEnd`). Circular references (an anchor referring to
  itself) are supported.
- **Anchors**: defining the same anchor name twice is rejected.

## Errors

Validation failures throw `YamlDotNet.Serialization.Preflight.PreflightValidationException`,
which exposes an `Errors` list of `PreflightValidationError`. Each error has a stable
`Code`, a `Message`, the `Start`/`End` marks of the offending event and, when a second
location is involved, `RelatedStart`/`RelatedEnd` marks plus a `RelatedMessage`.

| Code | Meaning |
| --- | --- |
| `EventLimitExceeded` | The input produced more events than `MaxBufferedEvents`. |
| `NestingLimitExceeded` | The input is nested deeper than `MaxDepth`. |
| `UnknownTag` | A tag was used that is not registered in the tag mappings. |
| `IncompatibleTaggedType` | A tag resolved to a type that is not assignable to the target type at that position. |
| `AmbiguousPolymorphicType` | The target type is abstract or an interface and no tag determines the concrete type. |
| `IncompatibleAlias` | An alias refers to an anchor whose type is incompatible with the target type at the alias position. Reports both the alias and the anchor marks. |
| `UnresolvableAlias` | An alias refers to an anchor that has not been defined. |
| `DuplicateAnchor` | The same anchor name is defined more than once. Reports both marks. |
| `UnexpectedEvent` | The event stream does not have the expected structure. |

## Notes

- Scalar values are convertible (for example a quoted number can be read into an `int`),
  so tag/type compatibility is enforced structurally on mappings, sequences and aliases,
  while scalars are checked for unknown tags and ambiguous polymorphic positions.
- The validation is performed against the static target type graph (properties, fields,
  collection element types, dictionary key/value types, nullable unwrapping). Positions
  typed as `object` accept any registered tag.

