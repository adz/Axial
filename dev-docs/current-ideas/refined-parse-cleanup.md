# Axial — Parse / Check / Refine / Schema cleanup

**Status:** implemented.

**Compatibility:** pre-1.0 compatibility does not constrain the design.

## 1. Semantic model

| Concern | Owner | Rule |
|---|---|---|
| Decode serialized input | `Parse` | Parsing changes representation and returns `ParseError`. |
| Test an existing typed value | `Check` | A check returns `unit`; it never replaces the value. |
| Describe a portable value restriction | `Constraint` | A constraint couples a check with inspectable metadata. |
| Construct an invariant-carrying type | `Refinement` | Construction follows checking and has a total reverse projection. |
| Perform a general typed mapping | conversion | Conversion may fail and need not describe subset admission. |
| Parse and validate structured boundaries | `Schema` | Schema owns paths, accumulation, reconstruction, and wire metadata. |
| Construct a domain model from a structured draft | `Schema.admit` | Admission preserves field structure while applying domain construction. |

These operations may compose, but their public names remain distinct. For example:

```text
Data --schema parse/admission--> Booking --refinement--> ValidBooking
```

The whole pipeline parses and refines; parsing itself is not refinement.

## 2. Package ownership

Target graph:

```text
Check   Parse   Data   Result      zero Axial dependencies
  |
Refined                            depends on Check
  |       |
  +-------+
      |
    Schema                         depends on Check, Parse, Refined, Data

ErrorHandling = metapackage(Check, Parse, Refined, Result)
```

- `Axial.Check` owns `Check`, `Constraint`, and portable constraint metadata.
- `Axial.Parse` owns `ParseError` and `Parse.*` and depends on no Axial package.
- `Axial.Refined` owns `Refinement` and named refined types.
- `Axial.Schema` owns `Schema`, `Schema.refine`, and `Schema.admit`.
- `Axial.Result` remains independent. Helpers that accept checks use structural function
  signatures rather than depending on `Axial.Check`.

This intentionally changes the current repository instruction that places parsing in
`Axial.Refined`. If this brief is accepted, update `AGENTS.md` and the package map in
`dev-docs/AGENT_INDEX.md` before implementation.

## 3. Operation routing

Route an operation by what it does, not merely because it can fail.

| Operation | API home |
|---|---|
| Test without changing the typed value | `Check` |
| Attach executable and inspectable restriction data | `Constraint` |
| Interpret serialized input | `Parse` |
| Extract `Some`, a choice, or a collection element into `Result` | `Result` |
| Normalize a typed value | ordinary function or explicit Schema normalization |
| Construct an invariant-carrying destination | `Refinement` |
| Perform arbitrary fallible typed mapping | conversion / `Schema.tryConvert` |
| Parse and check structured boundary data | `Schema` |

Consequences:

- `Check.String.trimmed` means “already trimmed” and returns `Ok ()`; it does not trim.
- Actual trimming is normalization, not parsing or checking.
- `Result.someOr` stays in Result because it extracts from `option` or returns an error.
- `required` and `optional` describe Schema field presence. Non-blank is a string
  constraint.

### Pipeline authoring rule

APIs intended for pipeline use place the primary transformed value last. Documentation,
tests, and public examples must then use the pipeline form consistently rather than
teaching an equivalent call-first style:

```fsharp
Schema.int
|> Schema.refine PositiveInt.refinement

input
|> Schema.parse bookingSchema
```

Use direct calls only for APIs that are not pipeline-oriented or where pipeline form
would obscure the operation.

## 4. Checks and Result guards

```fsharp
type Check<'value> =
    'value -> Result<unit,CheckFailure list>
```

Returning `unit` prevents checks from silently normalizing or replacing values.
`Check.all` runs every child against the same original value, accumulates failures, and
returns `Ok ()` only when all checks pass.

Value-preserving Result pipelines use:

```fsharp
Result.guard :
    ('value -> Result<unit,'error>) ->
    'value ->
    Result<'value,'error>
```

`Result.guard check value` returns the original value after a successful check. Its
structural signature keeps `Axial.Result` independent of `Axial.Check`.

There are no `check { }`, `refine { }`, or `parse { }` computation expressions. Use the
ordinary `result { }` builder and map errors explicitly when domains differ.

`Axial.Check.CheckDSL` provides structural `guard`, `orError`, and `mapError` adapters locally. They intentionally
mirror the corresponding Result helpers because `Axial.Check` cannot depend on `Axial.Result`. `guard` runs a check
and returns its original input after `Ok ()`; direct check calls still return `Result<unit,CheckFailure list>`.

## 5. Constraints

```fsharp
[<Sealed>]
type Constraint<'value> internal
    (
        check: Check<'value>,
        details: ConstraintDetails
    )

[<RequireQualifiedAccess>]
module Constraint =
    val check : Constraint<'value> -> Check<'value>
    val details : Constraint<'value> -> ConstraintDetails
    val checkAll : Constraint<'value> list -> Check<'value>

    val minLength : int -> Constraint<string>
    val maxLength : int -> Constraint<string>
    val lengthBetween : int -> int -> Constraint<string>
    val atLeast : 'value -> Constraint<'value>
    val atMost : 'value -> Constraint<'value>
    val greaterThan : 'value -> Constraint<'value>
    val lessThan : 'value -> Constraint<'value>
```

Built-ins cover portable restrictions shared by Check and Schema:

- non-blank, length bounds, email, already-trimmed, patterns, and known formats;
- inclusive and exclusive bounds, ranges, equality, and multiples;
- collection count bounds and distinctness;
- closed choices with portable literal values.

### Portable metadata

Do not use `obj` for constraint arguments. Use a closed, immutable model suitable for
structural equality, serialization, generators, AOT, and Fable:

```fsharp
type ConstraintArgument =
    | Text of string
    | Integer of int64
    | Decimal of decimal
    | Boolean of bool
    | List of ConstraintArgument list

type ConstraintDetails = {
    Code: string
    Arguments: Map<string,ConstraintArgument>
}
```

The concrete record and `Map` provide immutable, structural metadata rather than relying
on an interface-backed dictionary. Confirm `Map` and the final numeric cases against
Fable and JSON Schema before implementation. Reject blank codes, duplicate argument
names, invalid bounds, and reversed ranges.

Built-in constraints guarantee that metadata and executable behavior agree. Custom
constraints cannot prove that relationship; their metadata is author-declared:

```fsharp
Constraint.define :
    code: string ->
    arguments: (string * ConstraintArgument) seq ->
    check: Check<'value> ->
    Constraint<'value>
```

Reserve all built-in codes so custom constraints cannot impersonate standard semantics.
Generic interpreters expose unknown custom codes and arguments but emit standard JSON
Schema keywords only for built-ins or explicit interpreter extensions.

`Check.*`, `CheckDSL.*`, and Schema syntax delegate portable operations to the same
`Constraint` constructors. Pure combinators such as `Check.any`, conditional checks, and
failure mapping remain ordinary checks.

## 6. Parsing

Parsing stays function-shaped:

```fsharp
module Parse =
    val int : string -> Result<int,ParseError>
    val guid : string -> Result<Guid,ParseError>
    val dateTimeOffset : string -> Result<DateTimeOffset,ParseError>
    val optional :
        ('a -> Result<'b,'error>) ->
        'a option ->
        Result<'b option,'error>
```

There is no `Parser` type, parser registry, target-type lookup, or parser SRTP dispatch.
Custom parsers are ordinary functions.

```fsharp
result {
    let! quantity = Parse.int text
    let! positive = Refine.positiveInt quantity
    return positive
}
```

Schema primitive nodes continue to own their wire decoding. `Schema.int` does not route
through a parser attached to `Schema.text`.

`ParseError`, `CheckFailure`, and `SchemaErrors` remain separate. A check failure after a
successful parse is not a parse error. Remove `RefinementError` when it only wraps
`CheckFailure list`.

## 7. Refinement

A refinement admits a subset of an existing typed value into an invariant-carrying
destination:

```fsharp
[<Sealed>]
type Refinement<'underlying,'refined> internal
    (
        check: Check<'underlying>,
        constraints: Constraint<'underlying> list,
        construct: 'underlying -> 'refined,
        project: 'refined -> 'underlying
    )

[<RequireQualifiedAccess>]
module Refinement =
    val define :
        constraint': Constraint<'underlying> ->
        construct: ('underlying -> 'refined) ->
        project: ('refined -> 'underlying) ->
        Refinement<'underlying,'refined>

    val defineAll :
        constraints: Constraint<'underlying> list ->
        construct: ('underlying -> 'refined) ->
        project: ('refined -> 'underlying) ->
        Refinement<'underlying,'refined>

    val defineWithCheck :
        check: Check<'underlying> ->
        construct: ('underlying -> 'refined) ->
        project: ('refined -> 'underlying) ->
        Refinement<'underlying,'refined>

    val create :
        Refinement<'underlying,'refined> ->
        'underlying ->
        Result<'refined,CheckFailure list>

    val underlying :
        Refinement<'underlying,'refined> ->
        'refined ->
        'underlying

    val constraints :
        Refinement<'underlying,'refined> ->
        Constraint<'underlying> list
```

`define` is the normal single-constraint path. `defineAll` requires a non-empty list and
uses `Constraint.checkAll`. `defineWithCheck` is the explicitly metadata-free escape
hatch.

`Refinement.create` runs the check and invokes the total constructor only after `Ok ()`.
Normalization and arbitrary fallible mapping are not refinement.

### Law

```fsharp
Refinement.create refinement underlying = Ok refined
    implies
Refinement.underlying refinement refined = underlying
```

Use one canonical underlying representation per refinement. A collection refinement
should use a concrete representation such as `'a array`, not treat every `seq<'a>` as an
interchangeable underlying value.

## 8. Schema conversion, refinement, and admission

```fsharp
module Schema =
    val tryConvert :
        forward: ('a -> Result<'b,SchemaError list>) ->
        backward: ('b -> 'a) ->
        schema: Schema<'a> ->
        Schema<'b>

    val convert :
        forward: ('a -> 'b) ->
        backward: ('b -> 'a) ->
        schema: Schema<'a> ->
        Schema<'b>

    val refine :
        Refinement<'underlying,'refined> ->
        Schema<'underlying> ->
        Schema<'refined>

    val admit :
        create: ('draft -> Result<'domain,SchemaError list>) ->
        project: ('domain -> 'draft) ->
        draft: Schema<'draft> ->
        Schema<'domain>
```

- `tryConvert` is the fallible projected mapping.
- `convert` is the ordinary total mapping.

The names follow common F# convention: an unqualified function is total, while `try*`
signals expected failure. `convertTotal` is avoided because `Total` is uncommon in F#
API names.
- `refine` accepts only a genuine `Refinement` and retains its portable constraints.
- `admit` preserves structured fields and paths while applying domain construction.

All may share one internal projected-mapping node. Public names communicate different
operations.

A refinement-owned constraint executes once through `Refinement.create`; retained
constraint details are metadata and must not trigger duplicate execution. Remove duplicate
constraints from stock refined schema definitions.

`Schema.check` returns the canonical value rebuilt by the complete schema. It must not
discard constructor normalization.

## 9. Golden path: named refined primitive

The user owns the domain type and invariant; Axial provides the reusable machinery.

```fsharp
type PositiveInt =
    private
    | PositiveInt of int

    member this.Value =
        let (PositiveInt value) = this
        value

module PositiveInt =
    let refinement =
        Refinement.define
            (Constraint.greaterThan 0)
            PositiveInt
            _.Value

    let create value =
        Refinement.create refinement value
```

Schema use:

```fsharp
let quantitySchema : Schema<PositiveInt> =
    Schema.int
    |> Schema.refine PositiveInt.refinement

let parsed = Schema.parse quantitySchema input
```

The same constraint drives checking, diagnostics, inspection metadata, and applicable
interpreters. Encoding and inspection project through `PositiveInt.Value`.

## 10. Soundness and platform constraints

- Refined types use private constructors; construction occurs only after checks pass.
- `Refinement` constructors are total and projections obey the successful-projection law.
- No runtime reflection, `InternalsVisibleTo`, struct tricks, or units of measure form the
  foundation of the design.
- Public APIs and generated code must remain AOT-, trimming-, and Fable-safe.

## 11. Implementation checklist

### Check, Result, and Constraint

- [x] Restore `Check<'value> = 'value -> Result<unit,CheckFailure list>`.
- [x] Add structurally typed `Result.guard` without adding a Result-to-Check dependency.
- [x] Audit transforming checks; keep predicates in Check and move normalization out.
- [x] Keep extraction helpers such as `Result.someOr` in Result.
- [x] Move public typed `Constraint<'value>` from Schema to Check.
- [x] Replace `obj` metadata with a closed portable argument model.
- [x] Reserve built-in constraint codes and document custom metadata as author-declared.
- [x] Make Check DSL and Schema syntax delegate portable operations to Constraint.
- [x] Add inventory tests for built-in behavior, metadata, and interpreter coverage.

### Parse and Refined

- [x] Create independent `Axial.Parse`; move `ParseError` and `Parse.*` into it.
- [x] Update `AGENTS.md` and `dev-docs/AGENT_INDEX.md` when this direction is accepted.
- [x] Remove parsing refinements, string-to-primitive refinement instances, and parse/refine builders.
- [x] Replace arbitrary fallible `Refinement.define` with `define`, `defineAll`, and `defineWithCheck`.
- [x] Remove target strings and `RefinementError`; return `CheckFailure list` directly.
- [x] Reject empty `defineAll` input and test the successful-projection law.
- [x] Keep named `Refine.*` convenience functions and explicit refinement values.

### Schema

- [x] Rename the fallible mapping to `Schema.tryConvert` and keep `Schema.convert` for total mappings.
- [x] Lower conversion and refinement through one projected internal node where practical.
- [x] Execute refinement-owned constraints once while retaining their metadata.
- [x] Keep `Schema.admit` as structured draft-to-domain admission.
- [x] Make `Schema.check` return canonical reconstructed output.
- [x] Test normalization, constructor failure, root/path error lowering, encoding, and inspection.

### Narrow Schema refinement inference

Schema field blocks support both explicit and canonical refinement forms:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    refine ContactEmail.refinement
}

field "email" _.Email {
    withSchema Schema.text
    refine
}
```

The bare form is deliberately narrow SRTP inference. At that field site, Schema already knows the current underlying
type and getter destination type, and resolves one static
`Refinement : 'underlying * 'refined -> Refinement<'underlying,'refined>` contribution on the destination type.
The explicit form remains the escape hatch for local variants and clearer diagnostics.

This does not restore universal type-directed parsing, `Refine.from`, or parser/refinement bind dispatch. Parsing and
non-Schema refinement construction remain named, explicit operations.

### Documentation and conformance

- [x] Update source comments and generator inputs, then regenerate affected references.
- [x] Add API-shape tests for removed CEs, SRTP entries, and `RefinementError`.
- [x] Add API-shape tests for Constraint, Refinement, and conversion signatures.
- [x] Run focused Check, Result, Refined, Schema, generator, and API-shape tests.
- [x] Defer full documentation validation until the phase or release boundary.
- [x] Update `dev-docs/PLAN.md` and durable decisions only after this brief is accepted.

## 12. Rejected or deferred alternatives

- **Universal parser/refinement CEs:** hide operations behind target-type dispatch. Narrow Schema field inference is
  retained because both source and destination types are already fixed at that declaration site.
- **Linear parse/refinement/check error hierarchy:** conflates independent failures.
- **Checks returning their input:** permits hidden transformation; use `Result.guard`.
- **Arbitrary fallible refinement construction:** belongs to conversion.
