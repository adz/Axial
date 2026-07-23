# Schema And Refinement Unification

Status: implemented.

This proposal makes one refinement definition usable by direct type-directed refinement and by Schema. It also makes
Schema the only public API that accumulates path-aware validation failures. The standalone Diagnostics package,
`Validation<'value, 'error>`, and `validate { }` are removed.

The intended public model is:

- `Check<'value>` describes reusable constraints on one typed value.
- `Refinement<'raw, 'value>` describes fallible construction and total inspection.
- `Refine.from` constructs one value using a contributed refinement.
- `Schema<'value>` describes structured construction, inspection, validation, encoding, and metadata.
- `Schema.parse` and `Schema.check` accumulate independent failures and attach structural paths.
- `Result` and Flow handle application logic after Schema has admitted the input.

## Problem

Axial currently exposes overlapping validation systems:

- `Check<'value>` validates one typed value.
- `Refine.from` constructs one refined value.
- `Validation<'value, 'error>` accumulates arbitrary errors.
- `validate { }` provides both sequential and accumulating composition.
- `Diagnostics<'error>` stores path-aware failures.
- Schema parses, validates, constructs, checks, and already reports path-aware failures.

This overlap makes the input story harder to explain and harder to use:

- `Validation` duplicates Schema's main interpreter behavior without Schema's structural declaration.
- `validate { }` uses fail-fast `let!` and accumulating `and!` inside the same block.
- callers must add field paths manually around validation expressions;
- Schema repeatedly converts between `Result`, `Validation`, and `Diagnostics` internally;
- paths and accumulated errors live in a package separate from the declarations that generate them;
- `Schema.refine` takes construction and inspection functions separately even though `Refine.from` already resolves
  destination-specific construction;
- `fieldWith` combines field declaration with schema selection and prevents field configuration from reading as a
  regular pipeline.

The central observation is that Schema already performs Validation's useful job in most boundary code. Schema knows
the object fields, collection indexes, map keys, nested structure, constructor, and value schemas. It can attach paths
without asking application code to repeat those names.

## Responsibilities After The Refactor

### Result and Check

`Axial.Result` retains ordinary `Result` helpers, `result { }`, `Check<'value>`, and `CheckFailure`.

`Check<'value>` remains path-free and raw-input-free. A check can report several failures about one already-typed value.
Schema attaches a check or portable constraint to a structural node and supplies the node's path while interpreting it.

### Refined

`Axial.Refined` depends only on `Axial.Result`. It owns:

- `Refinement<'raw, 'value>`;
- `RefinementError`;
- `Refine.from`;
- `refine { }`;
- the built-in refined types and their refinement definitions.

Refined does not depend on Schema, paths, or accumulated schema errors.

### Schema

`Axial.Schema` depends on Data, Result, and Refined. It owns:

- `Schema<'value>`;
- schema declarations and interpreters;
- the opaque path representation;
- accumulated schema errors;
- parsing, checking, validation, writing, and codec plans.

Schema becomes the only public path-aware accumulating validation API.

### Removed Surface

The following public surface is removed:

- the `Axial.Diagnostics` package;
- `Diagnostics<'error>`;
- `Diagnostic<'error>`;
- public `PathSegment`;
- `Validation<'value, 'error>`;
- the `Validation` module;
- `validate { }` and its scope builders.

This is a pre-1.0 change. No compatibility aliases or deprecated copies remain.

## First-Class Refinements

A refinement needs both directions because direct construction uses the forward direction while Schema also checks,
writes, and compiles codecs using the reverse direction.

```fsharp
type Refinement<'raw, 'value>

[<RequireQualifiedAccess>]
module Refinement =
    val define :
        create: ('raw -> Result<'value, RefinementError>) ->
        inspect: ('value -> 'raw) ->
        Refinement<'raw, 'value>

    val create :
        Refinement<'raw, 'value> ->
        'raw ->
        Result<'value, RefinementError>

    val inspect :
        Refinement<'raw, 'value> ->
        'value ->
        'raw
```

A domain type defines its invariant once:

```fsharp
type Email =
    private
    | Email of string

[<RequireQualifiedAccess>]
module Email =
    let create raw =
        if String.contains "@" raw then
            Ok(Email raw)
        else
            Error(RefinementError.custom "email" "Expected an email address.")

    let value (Email value) =
        value

    let refinement =
        Refinement.define create value
```

The destination type contributes that definition to the type-directed protocol:

```fsharp
type Email with
    static member Refinement(_: string, _: Email) =
        Email.refinement
```

`Refine.from` resolves the contributed `Refinement<'raw, 'value>` and calls `Refinement.create`:

```fsharp
let email: Result<Email, RefinementError> =
    Refine.from rawEmail
```

The raw expression supplies `'raw`. The expected result supplies `'value`. Several raw representations for the same
destination remain possible because both types participate in static dispatch.

The `refine { }` builder uses the same protocol. There is no separate builder registry or construction-only protocol.

## Schema Refinement

`Schema.refine` accepts the same first-class definition:

```fsharp
Schema.refine :
    Refinement<'raw, 'value> ->
    Schema<'raw> ->
    Schema<'value>
```

Example:

```fsharp
let emailSchema =
    Schema.text
    |> Schema.refine Email.refinement
```

Schema stores both operations:

- construction runs while parsing raw structured input;
- inspection runs while checking existing values, writing Data, and compiling codecs.

Built-in refined schemas use the same `Refinement` values as `Refine.from`. Schema no longer duplicates built-in smart
constructor and projection pairs.

A domain type can contribute its canonical schema:

```fsharp
type Email with
    static member Schema(_: Email) =
        Schema.text
        |> Schema.refine Email.refinement
```

That makes an ordinary field declaration sufficient:

```fsharp
schema<Signup> {
    field "email" _.Email
    field "phone" _.Phone
    construct Signup.create
}
```

## Schema Validation

`Schema.validate` is the value-preserving form of refinement:

```fsharp
Schema.validate :
    ('value -> Result<unit, SchemaError>) ->
    Schema<'value> ->
    Schema<'value>
```

Its behavior is equivalent to constructing a refinement whose inspection function is `id` and whose construction
function returns the original value after the validation succeeds.

```fsharp
let validateCompanyEmail email =
    if Email.domain email = "example.com" then
        Ok()
    else
        Error(SchemaError.custom "company-email" "Expected an example.com address.")

let companyEmailSchema =
    Email.schema
    |> Schema.validate validateCompanyEmail
```

The validation function does not contain a field name or path. Schema supplies the current node's location:

- object fields supply string path components;
- list items supply integer path components;
- map entries supply string path components;
- nested schemas prefix the child path with the parent path;
- root validation uses the root path.

There is no separate public `Rule` abstraction. `Schema.validate` applies a result-producing function to the current
schema value.

Portable constraints remain distinct from executable validation. A portable constraint carries inspectable metadata
that JSON Schema, documentation, and UI interpreters can translate. An arbitrary validation function is executable but
cannot be translated automatically by those interpreters.

## Schema-Owned Errors

Schema operations return ordinary `Result` values:

```fsharp
Schema.parse :
    Schema<'value> ->
    Data ->
    Result<'value, SchemaErrors>

Schema.check :
    Schema<'value> ->
    'value ->
    Result<'value, SchemaErrors>
```

`SchemaErrors` replaces generic Diagnostics:

```fsharp
type SchemaErrors

[<RequireQualifiedAccess>]
module SchemaErrors =
    val toList : SchemaErrors -> SchemaIssue list
    val count : SchemaErrors -> int
    val isEmpty : SchemaErrors -> bool
    val toString : SchemaErrors -> string
```

A flattened issue contains its complete location:

```fsharp
type SchemaIssue =
    {
        Path: Path
        Error: SchemaError
    }
```

`Path` is opaque. Its internal representation distinguishes only string and integer components:

```fsharp
type Path

[<RequireQualifiedAccess>]
module Path =
    val root : Path
    val key : string -> Path
    val index : int -> Path
    val append : Path -> Path -> Path
    val format : Path -> string
    val fold :
        ('state -> string -> 'state) ->
        ('state -> int -> 'state) ->
        'state ->
        Path ->
        'state
```

There is no `Name` versus `Key` distinction. Both represent a string component. Schema knows whether a string came
from an object field or a map entry, but error lookup and rendering do not need different path cases.

`Path.fold` lets HTTP and other structural renderers distinguish keys from indexes without exposing a public segment
union.

Schema may retain a tree or non-empty collection internally for efficient prefixing and merging. That representation
is not a second public validation model.

## Internal Accumulation

Schema uses a private accumulator based on:

```fsharp
Result<'value, InternalSchemaErrors>
```

Private helpers provide:

- success;
- one error at the current path;
- mapping;
- combining two or more independent results;
- traversing lists and maps;
- prefixing paths;
- merging failures in deterministic path order while preserving multiple failures at the same path.

Schema interpreters stop converting through `Validation.fromResult`, `Validation.at`, and `Validation.toResult`.

Dependent stages remain fail-fast. In particular, a model constructor runs only after every independent field has
succeeded. Constructor-level validation cannot observe invalid or missing field values.

## Schema Computation Expression

Record schemas use one computation expression whose job is to separate fields and retain their typed constructor
arguments. Each field block transforms one `Schema<_>` value. Operations in one block cannot attach to the next field,
and Fantomas preserves the block structure.

```fsharp
let signupSchema =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            constrain required
            refine
            validate validateCompanyEmail
        }

        field "company-phone" _.Phone
        field "age" _.Age
        construct Signup.create
    }
```

The CE uses implicit yield. `field ...` and `construct ...` are expressions accepted by `SchemaBuilder.Yield`; users do
not write `yield`. This is the same F# computation-expression mechanism that permits expression-oriented builders such
as `seq { 1; 2 }`.

The outer builder handles only:

- ordered field declarations;
- the typed constructor argument chain;
- total or checked construction;
- creation of the erased field metadata and retained compiled record plan.

The optional inner field builder handles:

- selecting a schema with `withSchema`;
- applying portable constraints;
- applying a type-directed refinement;
- applying executable validation.

It is not a generic validation builder and has no `let!`, `and!`, or `return`.

### Fields Without A Block

The common declaration has no block:

```fsharp
schema<Person> {
    field "name" _.Name
    field "age" _.Age
    construct Person.create
}
```

The getter fixes the field type. The field resolves that type's canonical schema through `SchemaDefaults`, including a
type's contributed static `Schema` member. Moving to the next field commits the completed field.

The .NET-only name-inferred form remains available:

```fsharp
schema<Person> {
    field _.Name
    field _.Age
    construct Person.create
}
```

Fable declarations use explicit wire names because Fable cannot perform the quotation operation used to derive a
property name:

```fsharp
schema<Person> {
    field "name" _.Name
    field "age" _.Age
    construct Person.create
}
```

### Refinement Is Conditional

A field does not require a refinement when its selected schema already produces the getter type:

```fsharp
field "age" _.Age {
    withSchema Schema.int
    constrain (atLeast 18)
}
```

`Schema.int` produces `int`, and `_.Age` returns `int`, so the field is complete.

A refinement is required when the selected raw schema and getter have different result types:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain required
    refine
    validate validateCompanyEmail
}
```

Before `refine`, the current schema is `Schema<string>`. The parameterless `refine` operation uses the getter's
`Email` result type to resolve `Refinement<string, Email>`. After it runs, the current schema is `Schema<Email>`, so
`validateCompanyEmail` receives `Email`.

If `Email` contributes a canonical `Schema<Email>`, the entire block is unnecessary:

```fsharp
field "email" _.Email
```

### Field State And Operation Order

The field builder tracks three types:

- the record model;
- the getter's final field type;
- the current schema's output type.

`withSchema` establishes the current schema. `constrain` preserves its type. `refine` changes the current type from the
raw type to the getter type. `validate` preserves the current type.

This makes order visible and checked:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain required             // Constraint<string>
    refine                         // Schema<string> -> Schema<Email>
    validate validateCompanyEmail  // Email -> Result<unit, SchemaError>
}
```

A field block may finish without `refine` only when the current schema type equals the getter type. Otherwise the
compiler reports:

```text
A field block must finish with the getter type. Add `refine` after raw-schema operations.
```

Calling `refine` without a contributed `Refinement<'raw,'field>` fails at compile time. The error contains the missing
static `Refinement` signature. Compile-negative tests retain the exact diagnostic so later API changes cannot make this
failure silent or defer it to runtime.

### Type-Directed Refinement Resolution

F# cannot reliably resolve the static refinement inside the generic `refine` custom operation; doing so causes
non-uniform generic instantiation (`FS1198`). The operation therefore records a pending
`RefiningFieldDeclaration<'model,'raw,'field>`. The outer builder's inline `Yield` receives concrete raw and getter
types, resolves `Refinement<'raw,'field>`, and builds the final field schema.

This delay is an implementation detail. The public syntax remains parameterless:

```fsharp
refine
```

The design does not use reflection, a runtime registry, or an operator overload.

### Total And Checked Constructors

Both constructor forms close the same typed field chain:

```fsharp
schema<Signup> {
    field "email" _.Email
    field "age" _.Age
    construct Signup.create
}

schema<Signup> {
    field "email" _.Email
    field "age" _.Age
    constructResult Signup.createChecked
}
```

`construct` requires the fields to consume a constructor ending in `Signup`.
`constructResult` requires the fields to consume a constructor ending in `Result<Signup,string>`. Constructor
execution begins only after all independent field parsing, constraints, refinements, and validations succeed.

The field chain is recursive rather than arity-specific. The spike compiled a 12-field schema through the real JSON
interpreter; the representation has no fixed maximum.

### The Plain Function Model

The field operations correspond to ordinary `Schema<'value>` transformations. These functions remain public because
schemas are also built and reused outside records:

```fsharp
let companyEmailSchema =
    Schema.text
    |> Schema.constrain required
    |> Schema.refine Email.refinement
    |> Schema.validate validateCompanyEmail
```

The field block:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain required
    refine
    validate validateCompanyEmail
}
```

means:

```fsharp
Schema.text
|> Schema.constrain required
|> Schema.refine Email.refinement
|> Schema.validate validateCompanyEmail
```

followed by attaching that `Schema<Email>` to the `"email"` field and its getter.

This relationship is part of the public documentation. It explains the CE without exposing its internal field-chain
types. The record CE remains the sole record authoring syntax; Axial does not retain a second pipe-based record builder.

### `fieldWith` And `withSchema`

`fieldWith` is removed. Explicit schema selection uses the same `withSchema` operation in every field block:

```fsharp
schema<Node> {
    field "children" _.Children {
        withSchema (Schema.listWith nodeSchema)
    }

    construct Node.create
}
```

The name reads as an operation on the current field rather than a second kind of field declaration.

### Qualification

Normal schema code uses the opened builders directly:

```fsharp
let signupSchema =
    schema<Signup> {
        field "email" _.Email
        construct Signup.create
    }
```

`SchemaCE.schema` is only needed when a binding named `schema` hides the builder. Recursive definitions are the common
case:

```fsharp
let rec schema =
    Schema.delay (fun () ->
        SchemaCE.schema<Category> {
            field "children" _.Children {
                withSchema (Schema.listWith schema)
            }

            construct Category.create
        })
```

`field` and `construct` remain unqualified because the local binding does not hide them.

### Proven Portability

The spike established:

- exact nested syntax with implicit yield on .NET and Fable 5.6;
- optional field blocks;
- type-directed parameterless refinement;
- total and checked constructors through one field chain;
- successful parsing with the real Schema interpreter;
- JSON compilation, serialization, and deserialization through the retained typed record plan;
- a 12-field compiled record plan without arity overloads;
- NativeAOT publication and execution without CE-specific trim or reflection warnings.

The existing Fable rule remains: explicit wire names compile everywhere; quotation-derived names remain .NET-only.

## Constructor Validation

`construct` remains total. `constructResult` remains the fallible object constructor.

Constructor validation runs after all fields succeed. Its failure attaches to the current object path by default.
Existing relative constructor error placement remains only if adoption demonstrates that it is needed after fields own
their validation paths.

The first implementation retains one constructor failure. Support for several constructor failures requires a real
model that produces them; it must not reintroduce the public generic accumulator.

## Package Graph

The target dependency graph is:

```text
Axial.Result

Axial.Refined
  -> Axial.Result

Axial.Data

Axial.Schema
  -> Axial.Data
  -> Axial.Result
  -> Axial.Refined

Axial.Schema.*
  -> Axial.Schema
```

`Axial.ErrorHandling` installs Result and Refined. It no longer installs Diagnostics.

The `Axial` umbrella installs Data, Result, Refined, and Schema. It does not install Flow.

## Implementation Plan

Each commit below must leave the affected projects building and their focused tests passing.

### 1. Lock Current Type-Directed Refinement Behavior

Add public-behavior tests for built-in and user-contributed `Refine.from`, several raw types for one destination,
left-hand destination inference, `refine { }`, missing-instance compiler errors, AOT, and Fable.

Land or preserve the current uncommitted `Refine.from` work before changing its protocol.

### 2. Add `Refinement<'raw, 'value>`

Add the opaque type and its `define`, `create`, and `inspect` functions. Convert one built-in refined type internally
and prove that behavior remains unchanged.

### 3. Change Open Dispatch To Contributed Refinements

Replace construction-only `RefineFrom` dispatch with contributed `Refinement<'raw, 'value>` values. Make `Refine.from`
and `refine { }` resolve and apply the same definition. Remove the construction-only protocol in the same commit.

### 4. Convert The Built-In Refinement Catalogue

Give every built-in refined type one reusable refinement value. Route smart-constructor helpers through those values
where that removes duplication. Retain existing error behavior and inspection behavior.

### 5. Change `Schema.refine`

Replace separate construction, error mapping, and inspection parameters with one `Refinement` argument. Convert all
built-in refined schemas and schema tests. Prove that checking and codec writing still use the reverse direction.

### 6. Add `Schema.validate`

Add value-preserving validation over a schema node. Test root, field, nested, list, and map placement; independent
accumulation; checking existing values; and the separation between executable validation and portable constraints.

### 7. Add Schema-Owned Errors

Add opaque `Path`, `SchemaIssue`, and `SchemaErrors`. Change public Schema interpreters and adapters to return
`Result<'value, SchemaErrors>`. Initially adapt the existing Diagnostics representation internally so this commit can
remain small.

### 8. Replace Validation Inside Schema

Introduce the private Schema accumulator. Migrate primitive parsing, records, nested models, collections, options,
unions, enums, refinements, constraints, and constructors. Delete all Schema references to public Validation and
Diagnostics.

### 9. Add The Schema Computation Expression

Add `schema<'model> { }`, the outer typed field chain, implicit-yield field and constructor expressions, and the optional
inner field builder. Preserve the existing erased `FieldDescriptor` view and retained `ICompiledRecordPlan` view.

Support:

- explicit field names on every target;
- quotation-derived field names on .NET;
- fields without blocks through canonical schema resolution;
- `withSchema` blocks whose schema already returns the getter type;
- total and checked constructors;
- any field count through the recursive typed chain.

Add parse and compiled-codec tests before migrating declarations.

### 10. Add Field `constrain`, `refine`, And `validate`

Give the field builder typed state for its getter type and current schema type. `constrain` preserves the current type.
Parameterless `refine` records a pending raw-to-field conversion; the outer inline `Yield` resolves the contributed
`Refinement<'raw,'field>` after both types are concrete. `validate` preserves and checks the current type.

Add compile-negative tests for:

- a raw field block committed without `refine`;
- a missing contributed refinement;
- a constraint applied before or after the wrong transition;
- a validation function accepting the wrong stage;
- constructor order, arity, and argument type mismatches.

### 11. Migrate Schema Declarations And Remove The Pipe Builder

Update hand-written schemas, generated contracts, generator output, examples, tests, benchmarks, and reference
applications to `schema<'model> { }`. Emit explicit field names from generators so generated declarations compile on
.NET and Fable. Run Fantomas over migrated declarations.

Remove:

- `Schema.define`;
- `DefineShape` and `ObjectShape`;
- pipe-level `field`, `fieldWith`, `withSchema`, `constrain`, `construct`, and `constructResult`;
- the old shape chain after the CE's typed record plan has equivalent interpreter coverage.

Keep ordinary `Schema.constrain`, `Schema.refine`, and `Schema.validate` transformations for standalone and reusable
value schemas.

### 12. Remove Public Validation

Delete `Validation<'value, 'error>`, the Validation module, `validate { }`, scope builders, operators, and direct
Validation examples. Migrate remaining use sites to Result, Check, Refine, or Schema according to their actual role.

### 13. Remove The Diagnostics Package

Move remaining path formatting and Schema error rendering into Schema. Remove the project from solutions, package
references, umbrella packages, packing scripts, CI, API inventories, source inventories, and release manifests.

Verify that no public assembly signature mentions the removed package.

### 14. Rewrite Documentation

Update public XML comments and generator inputs before generated reference pages. Remove Diagnostics and Validation
guides rather than preserving old workflows. Rewrite the Refined guide around wrapper, smart constructor, inspection,
`Refinement.define`, contribution, `Refine.from`, and canonical Schema. Rewrite Schema guides around fields,
`withSchema`, refinement, validation, parsing, checking, writing, and `SchemaErrors`.

Update the handwritten product entry points and regenerate the site.

### 15. Run Phase Validation

Run focused and full tests, API-shape checks, package dependency checks, source inventory, .NET target builds, AOT,
Fable, generated docs, documentation validation, the site production build, and package creation.

## Testing Requirements

Tests assert public behavior rather than the private accumulator representation.

Refinement coverage includes:

- contributed definition resolution;
- construction and inspection;
- several raw types for one destination;
- built-in and user-defined types;
- direct and computation-expression use;
- missing and ambiguous definitions;
- AOT and Fable compatibility.

Schema coverage includes:

- exact successful values;
- complete ordered error lists;
- root, field, nested, list, and map paths;
- raw constraints before refinement;
- refined-value validation after refinement;
- constructor execution only after field success;
- parse and check consistency;
- write and parse round trips;
- compiled codec behavior;
- generated declarations;
- invalid field cursor transitions at compile time.

API-shape tests assert that `Validation`, `validate`, `Diagnostics`, `PathSegment`, and `fieldWith` are absent, while
`Refinement`, descriptor-based `Schema.refine`, `Schema.validate`, `withSchema`, `SchemaErrors`, and opaque `Path` are
present.

## Out Of Scope

This refactor does not add:

- effectful schema validation;
- database-backed uniqueness checks;
- remote policy evaluation;
- runtime reflection for schema or refinement discovery;
- automatic record discovery;
- another generic validation computation expression;
- public applicative accumulation functions;
- compatibility aliases for removed pre-1.0 APIs;
- automatic translation of arbitrary validation functions into JSON Schema;
- a second schema authoring language.

Effectful application rules run through Result or Flow after successful Schema admission.

## Before And After

This section covers every public API family affected by the proposal.

### Defining A User Refined Type

Before, direct type-directed refinement contributes only construction, while Schema receives construction and
inspection separately:

```fsharp
type Email with
    static member RefineFrom(raw: string, _: Email) =
        Email.create raw

let emailSchema =
    Schema.text
    |> Schema.refine Email.create mapEmailError Email.value
```

After, one definition serves both packages:

```fsharp
let refinement =
    Refinement.define Email.create Email.value

type Email with
    static member Refinement(_: string, _: Email) =
        Email.refinement

let emailSchema =
    Schema.text
    |> Schema.refine Email.refinement
```

Main benefit: construction and inspection cannot drift between Refined and Schema definitions.

### Direct Type-Directed Refinement

Before:

```fsharp
let email: Result<Email, RefinementError> =
    Refine.from rawEmail
```

After:

```fsharp
let email: Result<Email, RefinementError> =
    Refine.from rawEmail
```

Main benefit: the concise API remains unchanged while it now uses the same complete definition as Schema.

### Refinement Computation Expression

Before:

```fsharp
let result =
    refine {
        let! (email: Email) = rawEmail
        let! (phone: Phone) = rawPhone
        return Signup.create email phone
    }
```

After:

```fsharp
let result =
    refine {
        let! (email: Email) = rawEmail
        let! (phone: Phone) = rawPhone
        return Signup.create email phone
    }
```

Main benefit: the builder and `Refine.from` no longer have separate resolution behavior.

### Schema Refinement

Before:

```fsharp
Schema.text
|> Schema.refine Email.create mapEmailError Email.value
```

After:

```fsharp
Schema.text
|> Schema.refine Email.refinement
```

Main benefit: call sites pass one named domain definition instead of reconstructing the relationship from three
functions.

### Canonical Refined Fields

Before:

```fsharp
Schema.define<Signup>
|> fieldWith Email.schema "email" _.Email
|> fieldWith Phone.schema "phone" _.Phone
|> construct Signup.create
```

After:

```fsharp
schema<Signup> {
    field "email" _.Email
    field "phone" _.Phone
    construct Signup.create
}
```

Main benefit: domain types contribute their canonical schemas once, so object declarations contain only object
structure.

### Explicit Field Schema

Before:

```fsharp
Schema.define<Node>
|> fieldWith (Schema.listWith nodeSchema) "children" _.Children
|> construct Node.create
```

After:

```fsharp
schema<Node> {
    field "children" _.Children {
        withSchema (Schema.listWith nodeSchema)
    }

    construct Node.create
}
```

Main benefit: the field's local schema is visibly grouped and remains grouped after formatting.

### Plain Field With Constraints

Before:

```fsharp
Schema.define<Signup>
|> fieldWith Schema.int "age" _.Age
|> constrain (atLeast 18)
|> construct Signup.createForAge
```

After:

```fsharp
schema<Signup> {
    field "age" _.Age {
        withSchema Schema.int
        constrain (atLeast 18)
    }

    construct Signup.createForAge
}
```

Main benefit: `refine` is absent because `Schema.int` already produces the getter's `int` type. The block groups local
configuration without adding a conversion stage.

### Local Field Refinement With An Inferred Raw Schema

Before:

```fsharp
let emailSchema =
    Schema.text
    |> Schema.refine Email.create mapEmailError Email.value

Schema.define<Signup>
|> fieldWith emailSchema "email" _.Email
|> construct Signup.create
```

After:

```fsharp
schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
        refine
    }

    construct Signup.create
}
```

Main benefit: the getter supplies `Email`, the raw schema supplies `string`, and the parameterless operation resolves
`Refinement<string, Email>`.

### Local Field Refinement With A Configured Raw Schema

Before:

```fsharp
let emailSchema =
    Schema.text
    |> Schema.constrain required
    |> Schema.refine Email.create mapEmailError Email.value

Schema.define<Signup>
|> fieldWith emailSchema "email" _.Email
|> construct Signup.create
```

After:

```fsharp
schema<Signup> {
    field "email" _.Email {
        withSchema Schema.text
        constrain required
        refine
    }

    construct Signup.create
}
```

Main benefit: operation order shows that `required` applies to raw text before construction of `Email`, while the block
prevents later field operations from joining this field.

### Checked Record Construction

Before:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> field "age" _.Age
|> constructResult Signup.createChecked
```

After:

```fsharp
schema<Signup> {
    field "email" _.Email
    field "age" _.Age
    constructResult Signup.createChecked
}
```

Main benefit: total and checked construction use the same field syntax and retained compiled plan. The checked
constructor runs only after every field succeeds.

### Value-Preserving Schema Validation

Before, arbitrary field validation is commonly expressed outside Schema and manually wrapped with a path:

```fsharp
validate {
    let! email =
        validate.key "email" {
            return! validateCompanyEmail signup.Email
        }

    return email
}
```

After:

```fsharp
let companyEmailSchema =
    Email.schema
    |> Schema.validate validateCompanyEmail
```

Main benefit: the schema node supplies the path, and the same validation runs during both parsing and checking.

### Accumulating Independent Fields

Before:

```fsharp
validate {
    let! email =
        validate.key "email" {
            return! Refine.from rawEmail
        }

    and! phone =
        validate.key "phone" {
            return! Refine.from rawPhone
        }

    return Signup.create email phone
}
```

After:

```fsharp
data [
    "email" => rawEmail
    "phone" => rawPhone
]
|> Schema.parse signupSchema
```

Main benefit: field names are declared once in the schema, all independent failures accumulate automatically, and
callers do not manage paths or applicative syntax.

### Nested And Collection Validation

Before, callers scope each nested validation manually:

```fsharp
values
|> Validation.traverseIndexed (fun index value ->
    validate.index index {
        return! validateItem value
    })
```

After:

```fsharp
Schema.listWith itemSchema
|> Schema.parse input
```

Main benefit: the collection schema owns traversal and index paths, including every nested item failure.

### Parsing

Before:

```fsharp
Schema.parse signupSchema input
// Result<Signup, Diagnostics<SchemaError>>
```

After:

```fsharp
Schema.parse signupSchema input
// Result<Signup, SchemaErrors>
```

Main benefit: Schema no longer exposes an unrelated generic validation package in its primary result type.

### Checking Existing Values

Before:

```fsharp
Schema.check signupSchema signup
|> Validation.toResult
```

After:

```fsharp
Schema.check signupSchema signup
```

Main benefit: parse and check return the same ordinary result shape.

### Inspecting Errors

Before:

```fsharp
match result with
| Ok signup -> Ok signup
| Error diagnostics ->
    diagnostics
    |> Diagnostics.flatten
    |> List.iter renderDiagnostic
```

After:

```fsharp
match result with
| Ok signup -> Ok signup
| Error errors ->
    errors
    |> SchemaErrors.toList
    |> List.iter renderSchemaIssue
```

Main benefit: error inspection uses Schema terminology and a flat public reporting type while Schema remains free to
use an efficient tree internally.

### Rendering Errors

Before:

```fsharp
Diagnostics.toString diagnostics
```

After:

```fsharp
SchemaErrors.toString errors
```

Main benefit: rendering remains available without exposing generic graph construction and merging.

### Paths

Before:

```fsharp
let path =
    [
        PathSegment.Name "users"
        PathSegment.Index 0
        PathSegment.Key "email"
    ]
```

After:

```fsharp
let path =
    Path.key "users"
    |> Path.append (Path.index 0)
    |> Path.append (Path.key "email")
```

Main benefit: callers do not distinguish two string path cases that render and compare as the same location.

### Validation Construction Functions

Before:

```fsharp
Validation.ok value
Validation.succeed value
Validation.error diagnostics
Validation.fail diagnostics
Validation.fromResult result
Validation.toResult validation
```

After:

```fsharp
Ok value
Error schemaErrors
Schema.parse schema input
Schema.check schema value
```

Main benefit: ordinary Result represents success and failure; Schema owns creation of accumulated structural errors.

### Validation Mapping And Sequencing

Before:

```fsharp
validation |> Validation.map mapper
validation |> Validation.mapError mapError
validation |> Validation.bind next
validation |> Validation.ignore
validation |> Validation.orElse fallback
validation |> Validation.orElseWith fallback
```

After:

```fsharp
result |> Result.map mapper
result |> Result.mapError mapSchemaErrors
result |> Result.bind next
result |> Result.map ignore
result |> Result.orElse fallback
result |> Result.orElseWith fallback
```

Main benefit: fail-fast sequencing uses the standard Result vocabulary instead of a second wrapper whose `bind` does
not accumulate.

### Applicative Validation Functions

Before:

```fsharp
Validation.apply functionValidation valueValidation
Validation.map2 create left right
Validation.map3 create left middle right
Validation.merge left right
Validation.collect validations
Validation.sequence validations
Validation.traverseIndexed validateItem values
(mapper <!> validation)
(functionValidation <*> valueValidation)
```

After, these operations are private Schema interpreter machinery:

```fsharp
Schema.parse objectSchema input
Schema.parse (Schema.listWith itemSchema) input
Schema.check objectSchema value
```

Main benefit: users declare independent structure once; Schema performs accumulation without exposing applicative
plumbing or operators.

### Validation Path Functions

Before:

```fsharp
validation |> Validation.at path
validation |> Validation.name "email"
validation |> Validation.key "email"
validation |> Validation.index 0

validate.name "email" { ... }
validate.key "email" { ... }
validate.index 0 { ... }
validate.at path { ... }
```

After:

```fsharp
schema<Signup> {
    field "email" _.Email
    construct Signup.create
}

Schema.listWith itemSchema
Schema.mapWith valueSchema
```

Main benefit: object fields, list items, map entries, and nested schemas attach paths from their declarations. The
caller no longer repeats structure around validation code.

### Diagnostics Construction And Transformation

Before:

```fsharp
Diagnostics.empty
Diagnostics.singleton error
Diagnostics.merge left right
Diagnostics.map mapError diagnostics
Diagnostics.flatten diagnostics
Diagnostics.toString diagnostics
```

After:

```fsharp
Schema.parse schema input
Schema.check schema value
SchemaErrors.toList errors
SchemaErrors.toString errors
```

Main benefit: construction and merging become private implementation details. Public code receives only errors that a
Schema interpreter actually produced.

### Complete Boundary Example

Before:

```fsharp
let validateSignup rawEmail rawPhone =
    validate {
        let! email =
            validate.key "email" {
                return! Email.create rawEmail
            }

        and! phone =
            validate.key "phone" {
                return! Phone.create rawPhone
            }

        return Signup.create email phone
    }
    |> Validation.toResult
```

After:

```fsharp
let signupSchema =
    schema<Signup> {
        field "email" _.Email
        field "phone" _.Phone
        construct Signup.create
    }

let parseSignup rawEmail rawPhone =
    data [
        "email" => rawEmail
        "phone" => rawPhone
    ]
    |> Schema.parse signupSchema
```

Main benefits:

- field names exist once;
- refined types contribute construction and inspection once;
- parsing and checking share the same declaration;
- independent failures accumulate automatically;
- nested field and collection paths come from structure;
- callers receive an ordinary `Result`;
- Schema retains inspectable metadata for codecs, documentation, JSON Schema, and forms;
- no public Validation wrapper, diagnostic graph, path-segment union, or accumulating computation expression remains.
