# Schema Source Generation Sketch

Phase 16 status: deferred by design. Runtime reflection is rejected as a foundation, and source generation waits until
the explicit schema API has real consumers. This sketch fixes the generation *target* now so the tooling choice later is
mechanical.

## Decisions Captured

- **Reflection is deferred as a foundation.** The authored schema path stays AOT- and trimming-safe and
  Fable-compatible. Reflection may appear later only as an optional .NET import/tooling path.
- **Generation waits for API stability.** The pipeline builder is the public authoring surface (the CE was evaluated
  and not shipped), so a generator has exactly one target shape. Do not start generator tooling until schema consumers
  outside this repo exist.

## Generation Target

A `[<Schema>]` record would generate exactly what a developer writes by hand today — nothing more:

```fsharp
[<Schema>]
type Signup =
    { [<Required; MaxLength 254; Email>] Email: string
      [<AtLeast 13>] Age: int }

// generated:
module Signup =
    let schema : Schema<Signup> =
        Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> Schema.fieldWith
            [ SchemaConstraint.required; SchemaConstraint.maxLength 254; SchemaConstraint.email ]
            "email" _.Email Value.text
        |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.``int``
        |> Schema.build
```

- Constructor/getter alignment is generated from record field order: the constructor lambda lists fields in declaration
  order and each getter is the shorthand member access, so the compiler still checks alignment by argument position —
  generation adds no unchecked step. `tests/Axial.Schema.Tests/SchemaGenerationTargetProofTests.fs` compiles this exact
  target shape to keep it honest.
- Primitive field attributes lower one-to-one to existing `SchemaConstraint` values (`required`, `maxLength`, `email`,
  `minLength`, ranges, counts). No new constraint vocabulary is introduced by generation.
- External names default to camelCased field names with an attribute override.

## Private Constructors

Generated schemas can target private constructors **only when the generated code is emitted into the same module
scope as the type** (F# `private` on a representation means module/namespace-declaration-group scope). Practical rule:

- same-file emission (Myriad-style partial files do not exist in F#): not available — F# has no `partial` types, so a
  generator always emits a separate file.
- therefore: records with `private` representations cannot be targeted safely. The supported shapes are public records
  (everyday case) and `internal` representations paired with the generator emitting into the same assembly.
- smart-constructor domain types keep their hand-written `ValueSchema` via `Value.refined`; generation is for boundary
  records, not for refined domain values.

## Tooling Options (evaluate when reopened)

1. Myriad (F# AST plugin) — mature, but couples build to a third-party plugin.
2. A dedicated `dotnet` build task shipping with Axial — most control, most maintenance.
3. F# analyzers/codefix emitting checked-in code — zero build magic; generated code is reviewable and committed.

Option 3 is the current lean: generated-and-committed code keeps AOT/Fable guarantees trivially and needs no build
plumbing. Revisit when a consumer has enough boundary records for the boilerplate to hurt.
