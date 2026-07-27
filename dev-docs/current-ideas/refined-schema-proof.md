# Axial — Refined schema proof and safe record updates

**Status:** unresolved design exploration; split from `refined-parse-cleanup.md`.

**Prototype:** `dev-docs/current-ideas/prototypes/refined-private-record/`

## 1. Problem

A schema can reconstruct a record through `[<SchemaConstructor>]` and enforce cross-field invariants. The open question is how application code can retain that guarantee while keeping ordinary immutable record-update ergonomics.

Desired properties:

- application code uses the user's bare domain type, such as `Booking`;
- callers cannot construct or update an invalid `Booking` directly;
- user writes one record, not separate hand-written draft and domain records;
- updates use an immutable generated draft and native `{ draft with ... }` syntax;
- every update reruns field checks, normalization, and `[<SchemaConstructor>]`;
- user owns the domain constructor and invariant;
- generation remains compile-time, AOT-safe, trimming-safe, and Fable-safe.

F# cannot provide public record construction/update and enforced invariants on the same bare record type. Public record syntax permits invalid values. A private record preserves safety but removes external record construction and `{ booking with ... }`.

## 2. Current preferred semantic shape

The user writes one private domain record:

```fsharp
[<DeriveRefinedSchema>]
type Booking =
    private {
        Start: DateOnly
        End: DateOnly
    }

    [<SchemaConstructor>]
    static member Create(start: DateOnly, ``end``: DateOnly) =
        if start <= ``end`` then
            Ok {
                Start = start
                End = ``end``
            }
        else
            Error "Start must be on or before End"
```

Generation provides a public immutable draft with the same fields plus schema operations:

```fsharp
type BookingDraft = {
    Start: DateOnly
    End: DateOnly
}

module BookingSchema =
    val schema : Schema<Booking>
    val parse : Data -> Result<Booking,SchemaErrors>
    val check : Booking -> Result<Booking,SchemaErrors>

    val update :
        (BookingDraft -> BookingDraft) ->
        Booking ->
        Result<Booking,SchemaErrors>
```

Application code keeps the short, safe type:

```fsharp
let changeEnd newEnd (booking: Booking) =
    booking
    |> BookingSchema.update (fun draft ->
        { draft with End = newEnd })
```

Execution order:

```text
Booking
  -> generated projection
  -> BookingDraft
  -> user edit
  -> complete draft schema check/reconstruction
  -> user [<SchemaConstructor>]
  -> Booking
```

The draft is public because F# needs its record shape for `{ draft with ... }`. Constructing a draft grants no authority; only successful admission returns `Booking`.

## 3. Why other shapes are not preferred

### Public `Booking` plus generated `Booking.Checked`

This preserves direct record literals and updates, but safe application code must use the longer `Booking.Checked` type. The short `Booking` name then denotes the unsafe candidate and provides no invariant. A user alias improves spelling but does not fix the conceptual inversion.

This shape may remain useful for DTO or configuration snapshots where the public record is intentionally retained, but it is not the preferred domain-model path.

### User-written `BookingDraft` plus user-written `Booking`

This is sound and works with `Schema.admit`, but requires two user-maintained types. Generation contributes too little for the target ergonomic path. It remains the explicit option when draft and domain structures genuinely differ.

### Public `Booking` without retained evidence

This is ordinary `[<DeriveSchema>]`. It keeps native record syntax and leaves invariant discipline to the user. It remains valid when enforced domain safety is not required.

### Mutable generated editor

Rejected. Updates must stay immutable and use ordinary record-update syntax.

### Shared generic proof wrapper

Rejected as the default. It makes the wrapper, rather than the user-owned domain type, the safe value used throughout application code. Any public generic constructor accepting a caller-selected check also permits forged evidence.

## 4. Constructor accepting the generated draft

A draft-shaped constructor is attractive:

```fsharp
[<SchemaConstructor>]
static member Create(draft: BookingDraft) =
    if draft.Start <= draft.End then
        Ok {
            Start = draft.Start
            End = draft.End
        }
    else
        Error "Start must be on or before End"
```

It avoids repeating constructor parameters and lets invariant code work against one value. It creates a compile-order problem: `BookingDraft` must exist before the user source is compiled, but its shape is derived by parsing that source.

Schemagen can parse source before F# compilation, so this is possible in principle through two generated files:

```text
Booking.pre.g.fs    // BookingDraft; compiled before Booking.fs
Booking.fs          // user Booking and Create(BookingDraft)
Booking.post.g.fs   // projection, schema, parse/check/update; compiled after Booking.fs
```

Current `Axial.Schema.Contracts.Build.targets` inserts generated output immediately after its declaration file. Supporting a generated draft in the constructor therefore requires generator and MSBuild ordering changes.

Until that complexity is justified, constructor parameters remain the simpler shape:

```fsharp
static member Create(start: DateOnly, ``end``: DateOnly) = ...
```

## 5. Prototype findings

The compile prototype established:

1. Code in a later F# file can read fields from a `private` record.
2. External code cannot construct or record-update that private record.
3. A public generated draft supports inferred `{ draft with Field = value }` updates.
4. The generated update can project, edit, and invoke the user constructor successfully.
5. A user constructor can accept a generated draft when that draft is compiled before the user file.
6. A namespace-level type and same-named companion module cannot be split across files. A later generated `module Booking` conflicts with `type Booking` and produces `FS0250`.
7. Separate-file namespace type extensions also cannot recover the desired static API in this arrangement (`FS0644`), and type extensions cannot add a nested `Booking.Draft` type.

Therefore semantics are viable, but ideal names such as `Booking.update` and `Booking.Draft` conflict with current separate-file generation.

## 6. Open decisions

### Generated API name

Mechanically simple:

```fsharp
BookingSchema.schema
BookingSchema.parse
BookingSchema.check
BookingSchema.update
```

Desired but currently blocked by cross-file companion-module rules:

```fsharp
Booking.schema
Booking.parse
Booking.check
Booking.update
Booking.Draft
```

Decide whether `BookingSchema.update` is acceptable or whether generator output should be restructured to recover companion-module syntax.

### Draft name and visibility

Candidates:

```fsharp
BookingDraft
BookingSchema.Draft
```

Draft must be public enough for record-update syntax. Documentation should present it as transient edit/boundary data, not the domain model.

### Constructor shape

Choose between:

```fsharp
Booking.Create(start, ``end``)
```

and two-phase generation enabling:

```fsharp
Booking.Create(draft)
```

Field parameters keep tooling simple. Draft input reduces repetition but changes schemagen ordering and may affect editor/build behavior.

### Update validation path

`update` should run the edited draft through the complete draft schema before admission, not call only the aggregate constructor. This ensures field constraints and normalization also rerun. Tests must cover canonicalization, field-path errors, constructor errors, and rejected edits.

### Attribute scope

Likely split:

- `[<DeriveSchema>]`: ordinary public records; no persistent safety claim.
- `[<DeriveRefinedSchema>]`: private user-owned domain record plus generated draft and safe operations.

Confirm whether generated checked wrappers for intentionally public records deserve a separate later feature. Do not overload the first implementation with both models.

## 7. Relationship to accepted cleanup

This exploration does not block the independent cleanup in `refined-parse-cleanup.md`:

- unit-returning `Check`;
- portable `Constraint` metadata;
- independent `Axial.Parse`;
- `Refinement` and `Schema.refine` for named invariant-carrying types;
- `Schema.convert`, `Schema.tryConvert`, and `Schema.admit`;
- canonical reconstruction through `Schema.check`.

`Schema.refine` remains useful for types such as `PositiveInt`. `Schema.admit` remains useful when draft and domain shapes genuinely differ. This document covers generated same-shape domain records plus safe immutable editing only.

## 8. Next prototype

Prototype against actual schemagen rather than a standalone project:

1. Generate public draft from a private attributed record.
2. Emit schema/update under `BookingSchema` using current post-source ordering.
3. Verify FCS discovery, command-line build, incremental build, IDE design-time build, AOT, and Fable.
4. Compare field-parameter constructor against pre-source generated draft constructor.
5. Decide whether improved `Booking.*` naming warrants generator restructuring.

Do not promote this design into `PLAN.md` or implementation tasks until these choices are resolved.
