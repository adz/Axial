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

## Additional Generation Targets Found While Designing `Model.construct` (2026-07-11)

Trying to add a typed, checked "construct a model from already-typed field values" function
(`Model.construct schema myStart myEnd`) as an ordinary library function surfaced a hard wall: `Schema<'model>`
only carries `'model` in its type, never per-field type information, so no library function signature can verify
caller-supplied positional arguments against a *specific* schema's fields at compile time. Every attempt that
preserved full type safety required operating on the pre-erasure `SchemaBuilder` chain at declaration time (real,
but adds ceremony); every attempt that avoided that ceremony required either runtime reflection (rejected
elsewhere in this doc, and CI-enforced against via the NativeAOT probe and Fable JS-surface check) or numbered
arity-capped overloads (rejected as inconsistent with "a schema is a smart constructor," not a small workaround).

This is exactly the shape of problem source generation exists to solve: a generator has full static knowledge of a
*specific* record's field names and types at generation time, the same knowledge a hand-written function can never
recover from `Schema<'model>` alone. Two additional generation targets follow from this, beyond the `schema` value
already sketched above:

### Generate the checked constructor directly

```fsharp
module Signup =
    let schema : Schema<Signup> = ...          // as sketched above
    let construct (email: string) (age: int) : Result<Signup, Diagnostics<SchemaError>> =
        // generated: runs each argument through that field's SchemaConstraints (same SchemaConstraintCheck
        // lowering Model.parse uses), then invokes the record constructor if every field passed.
        ...
```

No new abstraction is needed on the library side beyond what already exists (`SchemaConstraintCheck`,
`ConstructorApplication.TryApplyTrusted`) — the generator just emits the correctly-typed, per-type version of the
factory walk that a library function cannot express generically. This is the only path found (across an extended
design session — see `dev-docs/decisions/README.md` for the summary) that gets back to `Signup.Construct(email,
age)`-style ergonomics without giving up compile-time safety or breaking AOT/Fable.

### Generate per-field optics

```fsharp
module Signup =
    type Field<'value> = { Name: string; Get: Signup -> 'value }
    let email : Field<string> = { Name = "email"; Get = _.Email }
    let age : Field<int> = { Name = "age"; Get = _.Age }
```

A generated optic per field — name plus typed getter, both already known to the generator from the record
declaration — is a reusable, compile-time-safe, reflection-free "reference to this field of this model," usable
anywhere a field currently has to be named twice (once in the schema, once again wherever a caller wants to talk
about it):

- **Contextual rules** (parked design problem, see `dev-docs/decisions/README.md`): `Rules.field Signup.email rule`
  can derive the diagnostics path from the optic instead of a hand-typed string that can silently drift from the
  schema's actual field name.
- **Redisplay / UI binding**: the schema's own stated ambition (ActiveModel-style form binding) needs exactly this
  — a typed way to say "this UI field maps to this model field" without stringly-typed lookups.
- **Contract migrations** (`schema-contract-versioning.md`): a migration currently pattern-matches the previous
  version's record directly (`fun (v1: V1.Signup) -> Ok { Email = v1.Email; Age = 18 }`). Generated optics on both
  the source and target version records would let migrations be written and checked against named fields rather
  than raw record literals, and — combined with the generated `construct` above — let a migration's *output* be
  checked-constructed against the target version's schema rather than assembled as an unchecked record literal
  (see the cross-reference added to that doc).

### Why this doesn't relax the Private Constructors section above

Both of these are per-*type* codegen, same as the `schema` value itself — they inherit exactly the same limitation
already recorded above: a generator emitting into a separate file cannot target a `private` representation. That
keeps the existing boundary intact: generation targets *boundary/version records* (public), never the private,
hand-written domain type. A generated `construct`/optic pair for a private type would need the same resolution
already flagged as a future direction — an opaque, library-owned wrapper (`Trusted<'model>`, see
`dev-docs/decisions/README.md`) whose constructor lives inside `Axial.Schema`'s own module rather than the
generated file, sidestepping the file-scope problem entirely rather than fighting it.

## Tooling Options (evaluate when reopened)

1. Myriad (F# AST plugin) — mature, but couples build to a third-party plugin.
2. A dedicated `dotnet` build task shipping with Axial — most control, most maintenance.
3. F# analyzers/codefix emitting checked-in code — zero build magic; generated code is reviewable and committed.

Option 3 is the current lean: generated-and-committed code keeps AOT/Fable guarantees trivially and needs no build
plumbing. Revisit when a consumer has enough boundary records for the boilerplate to hurt.
