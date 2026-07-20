---
weight: 2
title: Getting Started
type: docs
description: Declare what your data looks like once; parse, check, document, and encode from that one declaration.
---

# Getting started with Schema

Every application boundary answers the same questions: what fields does this input have, what are
their types, what makes a value acceptable, and how do I build my domain type from it? Most codebases
answer those questions several times over — once in a DTO parser, again in validation rules, again in
API documentation, again in test fixtures — and the answers drift apart.

`Axial.Schema` lets you answer them once. A schema is an ordinary F# value that *describes* a type:
its fields, their wire names, their constraints, and how a validated value gets constructed. Everything
else — parsing input, checking existing values, generating JSON Schema documents, compiling fast JSON
codecs, driving forms — is an *interpreter* that reads that description. One declaration, several
behaviors, no drift.

This page walks the whole loop: declare a schema, feed it input, read its errors, and see what else
the same declaration can do.

## Your first schema

A schema for a record names each field, points at its value with a getter, and finishes with the
constructor:

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

type Signup =
    { Email: string
      Age: int }

let signupSchema : Schema<Signup> =
    Schema.define<Signup>
    |> field "email" _.Email
    |> constrain emailFormat
    |> field "age" _.Age
    |> constrain (atLeast 13)
    |> construct (fun email age -> { Email = email; Age = age })
```

Read it top to bottom:

- `Schema.define<Signup>` starts the declaration. It is not yet a schema — it doesn't know how to
  build a `Signup`.
- Each `field` declares one field: the wire name (`"email"` is what appears in JSON, form posts, and
  error paths) and the getter (`_.Email` is how the schema reads the value back out for checking and
  encoding). The field's schema is inferred from the getter's type — `string` and `int` here.
- `constrain` attaches a rule to the field directly above it. Constraints are typed: putting
  `emailFormat` (a text constraint) after an `int` field is a compile error on that line, not a
  runtime surprise.
- `construct` closes the declaration with the constructor. Its arguments must match the declared
  fields in order and type — checked by the compiler — and there is no limit on how many fields a
  schema can have.

If a constructor should be able to *reject* a combination of otherwise-valid fields (say, a date range
whose end precedes its start), close with `constructResult` and return `Result<'model, string>`
instead. The [syntax guide](syntax.md) covers every form, including fields whose schemas can't be
inferred.

## Give it input: Data

A schema parses *structured data*, not strings of JSON. The input type is
[`Data`]({{< relref "/data/" >}}) — a small, source-neutral tree of objects, lists, text, numbers,
booleans, and nulls. Anything that can produce that shape can feed a schema: JSON, form posts, CLI
arguments, configuration, or values you write by hand.

The quickest way to write `Data` by hand is the builder syntax:

```fsharp
open Axial
open Axial.Data.Syntax

let goodInput =
    data [
        "email" => "ada@example.com"
        "age" => 42
    ]
```

which is handy in tests and examples. Real boundaries usually convert from a source instead:

```fsharp
// from System.Text.Json, e.g. an HTTP request body
let fromJson = Data.ofJsonDocument jsonDocument

// from name/value pairs, e.g. a form post or query string
let fromForm = Data.ofNameValues [ "email", "ada@example.com"; "age", "42" ]

// from a map of raw strings
let fromMap = Data.ofMap (Map.ofList [ "email", "ada@example.com"; "age", "42" ])
```

Note that `"42"` arriving as text is fine: parsing performs shape conversion, so a number field
accepts a numeric token whether the source delivered it as JSON `42` or form-post `"42"`.
`Data` is its own package with no dependencies, useful on its own for shaping test fixtures — see
[its docs]({{< relref "/data/" >}}).

## Parse it

```fsharp
match Schema.parse signupSchema goodInput with
| Ok signup -> printfn "Welcome, %s" signup.Email
| Error diagnostics ->
    diagnostics
    |> Axial.Validation.Diagnostics.flatten
    |> List.iter (SchemaError.renderDiagnostic >> printfn "%s")
```

One call does everything the declaration promised: checks the shape, converts values, runs every
constraint, and — only if all fields succeeded — invokes your constructor. On failure you get
*diagnostics with paths*, not a single message: an input with a blank email and an age of `4` reports
both problems, each addressed to its field (`email`, `age`), ready to render next to the right form
input. Nested records, lists, and maps extend the path (`address.city`, `lines[2].quantity`).

When a boundary also needs to redisplay what the user originally typed — form round-trips, audit
logs — use `Schema.parseRetainingInput`, which keeps the raw `Data` alongside the parse result. See
[redisplay and field errors](redisplay-and-field-errors.md).

## Check a value you already have

Parsing is for structured input. Sometimes a value arrives already assembled — from a database mapper,
a message queue, or another team's code — and you want the same guarantees:

```fsharp
let imported = { Email = "ada@example.com"; Age = 42 }

match Schema.check signupSchema imported with
| Ok trusted -> save trusted
| Error diagnostics -> reject diagnostics
```

`Schema.check` reads each field back through its getter, runs the same constraints, and re-runs the
constructor if it is a checked one. Same declaration, same rules, different entry point.

## What one declaration buys you

Everything below reads the `signupSchema` value declared above — none of it requires further
annotation:

```fsharp
// A JSON Schema document for your API docs or contract tests
// (package Axial.Schema.JsonSchema; same Axial.Schema namespace)
let contract = JsonSchema.generate signupSchema

// A compiled JSON codec for trusted hot-path serialization
open Axial.Schema.Codec // package Axial.Schema.Codec

let codec = Json.compile signupSchema
let json = Json.serialize codec { Email = "ada@example.com"; Age = 42 }

// Introspectable metadata: fields, wire names, constraints
let description = Inspect.model signupSchema
description.Fields |> List.map _.Name   // ["email"; "age"]
```

The pattern to internalize: a schema never *does* anything — it is data describing your data.
Interpreters do things. When you need new behavior over your models (a new output format, a UI
generator, a fuzzer), you write one interpreter over `Inspect` metadata and every schema in your
codebase gains that behavior at once.

## What parsing does and does not prove

`Schema.parse` proves *this input* passed the declaration and was constructed through it.
`Schema.check` proves the same for *this value*. Neither changes what ordinary F# construction allows:
if `Signup` is a public record, any code can still write `{ Email = ""; Age = 4 }` directly.

That is a language-level fact, and Axial's answer is to choose representations deliberately rather
than sprinkle validation calls: plain records for wire contracts and drafts, private refined types for
intrinsic invariants, smart constructors where cross-field invariants must hold everywhere. The
[trusted construction guide](trusted-construction.md) develops the options; the
[refined values guide](refined-values.md) shows fallible constructors inside schemas.

## Where to go next

- [Schema syntax](syntax.md) — every declaration form: inference, explicit schemas, bare getters,
  options, lists, maps, unions.
- [Input sources](input-sources.md) — the full menu of `Data` conversions.
- [Construction guarantees](trusted-construction.md) — decide what parsing proves and when the type itself must
  preserve an invariant.
- [Runnable examples](examples.md) — complete executable programs, mirrored from real code.
- [Patterns](patterns/) — project layouts for wire/domain splits, private aggregates, and
  schema-derived tests.
