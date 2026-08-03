# Format Packages And The JSON Platform Runtime

Status: proposed. Extracted from `project-split.md`, which now covers repository structure only. Nothing here
depends on the repository split, and the split does not depend on any of it.

This covers two related questions: how representation formats are packaged, and how one JSON package serves both
.NET and Fable without either platform paying for the other's representation.

## One Package Per Format

Future representation formats should use separate packages:

```text
Axial.Schema.Json
Axial.Schema.Xml
Axial.Schema.Yaml
Axial.Schema.Toml
Axial.Schema.MessagePack
Axial.Schema.Protobuf
```

This keeps transitive dependencies small and allows each format to have its own wire rules, limitations, runtime
support, release timing, and performance work.

Do not add empty packages in anticipation of demand. Create a package only when its format has an implemented consumer
and tests.

Do not create a public format-neutral package merely to hold interfaces. First prove that two or more formats share
substantial code with the same semantics.

If shared compiler machinery emerges, keep it internal to the repository or in an internal package until its boundary
is stable. Sharing the word "codec" is not enough reason for a public abstraction.

### Format Packages Are Not Interchangeable

JSON, XML, YAML, TOML, MessagePack, and Protobuf do not share the same data model.

Examples of format-specific differences include:

- object key and field-name rules;
- attributes versus elements in XML;
- aliases, anchors, and scalar resolution in YAML;
- table structure in TOML;
- integer widths and binary values in MessagePack;
- field numbers, unknown fields, and compatibility rules in Protobuf;
- streaming and framing behavior;
- canonical encoding and ordering;
- null, missing, optional, and default semantics.

Each package should state which Schema shapes and constraints it supports. Unsupported shapes should fail during codec
compilation with a typed error, not later while encoding a value.

## Shared Compiler, Platform-Specific JSON Runtime

`Axial.Schema.Json` should keep one public API and one schema-to-codec compiler.

The compiler walks Schema's retained typed shape and builds a reusable encoding and decoding plan. This logic should be
shared across .NET and Fable.

The runtime that executes the plan should be optimized for its platform.

```text
Schema<'value>
      ↓
shared JSON plan compiler
      ↓
platform runtime primitives
   ├── .NET UTF-8/span implementation
   └── Fable JavaScript implementation
```

Do not publish separate `.NET` and `JavaScript` NuGet packages at this stage. Platform selection is a compilation
detail, and users should write against the same `Json.compile`, serialize, and deserialize API.

## Fable Build Constraint

This repository cannot reliably select different F# source files for .NET and Fable compilation. Fable project
cracking has not made conditional file inclusion dependable.

Platform differences therefore must use inline compiler directives. Keep those directives concentrated in platform
modules rather than spreading them throughout codec compilation and parsing logic.

A file may define the same module twice, with only one implementation active:

```fsharp
#if FABLE_COMPILER
module internal JsonPlatform =
    // JavaScript implementation
#else
module internal JsonPlatform =
    // .NET implementation
#endif
```

Other files call `JsonPlatform` without their own compiler directives.

This follows the existing `Axial.Schema.Platform` pattern. The pattern is a response to the build constraint, not a
claim that .NET and JavaScript should use the same low-level representation.

## What Belongs In `Platform.fs`

Use a platform module for small operations that have the same meaning but different implementations:

- invariant integer and decimal parsing;
- UTF-8 string conversion;
- byte comparison and scanning;
- buffer rental and return;
- bounded byte slices;
- encoding string slices;
- exception construction where platform support differs;
- checks that depend on erased or retained runtime generic information.

Keep the call signatures platform-neutral when that does not damage the fast path.

Do not wrap every BCL call. A wrapper is useful when it removes a compiler directive from business or codec logic, or
when the operation requires different platform behavior.

## When To Use A Larger Conditional Runtime Module

Some differences are too large for a collection of tiny wrappers. In that case, place two implementations of a
coherent internal module behind one `#if` boundary in the same file.

Examples include:

- the input cursor;
- the output writer;
- JSON string escaping and unescaping;
- number parsing and formatting;
- property-name matching;
- stream integration;
- JavaScript-native string or typed-array integration.

The rest of the codec should depend on a small internal runtime surface. It should not know which implementation was
compiled.

Do not create one very large `Platform.fs` containing unrelated subsystems. Prefer focused modules such as
`JsonBufferPlatform`, `JsonNumberPlatform`, and `JsonTextPlatform` when the runtime grows.

## .NET JSON Runtime

The .NET implementation should operate directly on UTF-8 wherever the public input permits it.

Use appropriate .NET primitives such as:

- `ReadOnlySpan<byte>` for bounded parsing;
- `Span<byte>` for formatting into owned buffers;
- `Utf8Parser` and `Utf8Formatter` for supported primitives;
- `IBufferWriter<byte>` for caller-owned output;
- `ArrayPool<byte>` for temporary buffers;
- cached UTF-8 field names;
- direct stream or pipe adapters where they avoid intermediate strings.

Avoid converting a complete UTF-8 payload to `string` before parsing. Avoid allocating a new `byte[]` merely to pass a
slice when a span can represent it.

The current byte-array cursor is a useful portable baseline. The refactor should allow the .NET runtime to use spans
more directly without forcing span types into the shared public API or the Fable implementation.

Public .NET overloads may expose `ReadOnlySpan<byte>`, `ReadOnlyMemory<byte>`, `IBufferWriter<byte>`, `Stream`, or
`PipeReader` when each has a demonstrated use. Keep them behind `!FABLE_COMPILER` when Fable cannot represent them.

Do not make a ref-struct type part of a shared internal interface that Fable must compile.

## Fable JSON Runtime

The Fable implementation should use JavaScript's actual performance model rather than emulating .NET spans.

Candidate representations include JavaScript strings, `Uint8Array`, `TextEncoder`, and `TextDecoder`. Choose through
benchmarks and required interoperability, not by matching the .NET implementation mechanically.

If most Fable callers begin with a JavaScript string, a string-native decoder may be better than converting the entire
value to UTF-8 bytes first. If callers handle network or binary buffers, a typed-array path may be worthwhile.

The public behavior must match .NET for supported Schema shapes:

- field names and escaping;
- missing and unknown fields;
- duplicate-field policy;
- number ranges and failures;
- null and option semantics;
- discriminated union representation;
- map keys;
- date, time, GUID, and decimal formatting where supported;
- error paths and useful diagnostic text.

Identical implementation is not required. Equivalent documented behavior is required.

## Current Fable Status And Remaining Work

`Axial.Schema.Json` is a supported Fable surface. The benchmark uses the current Schema API,
`scripts/check-fable-js-surface.sh` passes, CI runs it, and generated JavaScript executes a Node encode/decode round
trip. Stream APIs remain .NET-only.

Further platform-runtime work should strengthen the shared semantic suite rather than re-prove basic support:

1. expand cross-platform golden cases for strings, numbers, nulls, options, lists, maps, records, and unions;
2. add decimal edge cases and reject syntax that differs unintentionally;
3. keep .NET-only APIs, such as streams, explicit in the documentation;
4. keep the Fable check in CI for every codec change.

## Performance Validation

Do not choose the platform abstraction from intuition alone. Benchmark the operations that dominate real payloads.

The .NET suite should measure:

- decode from UTF-8 bytes;
- decode from `ReadOnlySpan<byte>` where exposed;
- encode to caller-owned `IBufferWriter<byte>`;
- encode to string;
- stream encode and decode;
- allocation counts;
- field matching for small and large records;
- nested records, lists, maps, and unions;
- comparison with `System.Text.Json` source generation.

The Fable suite should measure:

- decode from string;
- decode from `Uint8Array` if supported;
- encode to string;
- encode to `Uint8Array` if supported;
- conversion cost between strings and UTF-8;
- comparison with native `JSON.parse` and `JSON.stringify` for equivalent behavior.

Keep platform-specific fast paths behind the same semantic tests. A faster implementation that accepts or emits a
different contract is a compatibility change, not an optimization.


## Implementation Sequence

Not started. This was Phase 2 of the repository split; it is not a precondition for it.

1. Inventory every `#if` in the current codec.
2. Classify each branch as a small platform primitive, a coherent runtime subsystem, or a public .NET-only API.
3. Move small primitives into focused platform modules.
4. Place larger alternative implementations behind one conditional module boundary per subsystem.
5. Keep the shared schema compiler free of platform directives.
6. Preserve the passing Fable benchmark and Node round trip.
7. Expand cross-platform semantic golden tests.
8. Benchmark .NET span paths and JavaScript-native paths.
9. Optimize each backend without changing the shared behavior.

Do not block the repository split on any of this. Require a passing baseline and a design that does not scatter new
conditionals.

## Risks

### Platform Abstraction Reduces .NET Performance

Mitigation: keep span-heavy work inside the .NET runtime module, benchmark allocations and throughput, and avoid
platform-neutral interfaces that require copying.

### Fable Behavior Silently Differs

Mitigation: run shared golden cases in .NET and generated JavaScript, especially for decimal, escaping, missing
values, and numeric ranges.

## Decisions

- Each future format gets its own package.
- One JSON package serves .NET and Fable.
- The schema-to-codec compiler is shared.
- Runtime implementations are platform-specific internally.
- Compiler directives are concentrated in platform modules because conditional source inclusion is not dependable.

## Choices To Resolve

- which .NET byte, memory, writer, stream, and pipe overloads belong in the first release;
- whether Fable's primary representation is string, `Uint8Array`, or both.
