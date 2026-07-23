# Schema And Refinement Unification

Status: proposed pre-1.0 refactor.

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
let email: Email =
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
Schema.define<Signup>
|> field "email" _.Email
|> field "phone" _.Phone
|> construct Signup.create
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
```

There is no `Name` versus `Key` distinction. Both represent a string component. Schema knows whether a string came
from an object field or a map entry, but error lookup and rendering do not need different path cases.

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
- merging failures in deterministic declaration order.

Schema interpreters stop converting through `Validation.fromResult`, `Validation.at`, and `Validation.toResult`.

Dependent stages remain fail-fast. In particular, a model constructor runs only after every independent field has
succeeded. Constructor-level validation cannot observe invalid or missing field values.

## Field Pipelines

`fieldWith` is removed. `field` declares the external name, getter, and expected model field type. Schema selection and
value operations follow through the current-field cursor.

The default form remains short:

```fsharp
Schema.define<Person>
|> field "name" _.Name
|> field "age" _.Age
|> construct Person.create
```

The next `field` or `construct` commits the current field and resolves its canonical schema.

`withSchema` supplies a local schema:

```fsharp
Schema.define<Node>
|> field "children" _.Children
|> withSchema (Schema.listWith nodeSchema)
|> construct Node.create
```

Field-level refinement can infer the default raw schema from the refinement's raw type:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> refine Email.refinement
|> construct Signup.create
```

Here `Email.refinement : Refinement<string, Email>` supplies `string`, so the cursor resolves `Schema.text`.

`withSchema` is only needed when the raw schema differs from its default or carries local configuration:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> withSchema configuredEmailText
|> refine Email.refinement
|> validate validateCompanyEmail
|> construct Signup.create
```

Pipeline order identifies the layer being configured:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> withSchema Schema.text
|> constrain required
|> refine Email.refinement
|> validate validateCompanyEmail
|> construct Signup.create
```

In this example, `required` applies to the raw string and `validateCompanyEmail` applies to `Email`.

The cursor tracks the selected schema's output type and the model getter's type. Moving to another field or calling
`construct` fails to compile while those types differ.

### Formatting Constraint

A flat pipeline contains no syntax-level grouping. Fantomas can align all pipeline operators even if a developer
manually indents `refine`, `validate`, or `constrain` under the preceding field. The API must not assign meaning to that
indentation.

Canonical domain schemas keep the common declaration free of field sub-pipelines:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> field "phone" _.Phone
|> construct Signup.create
```

Long local configurations should use a named schema value so formatting reflects real nesting:

```fsharp
let companyEmailSchema =
    Schema.text
    |> Schema.constrain required
    |> Schema.refine Email.refinement
    |> Schema.validate validateCompanyEmail

let signupSchema =
    Schema.define<Signup>
    |> field "email" _.Email
    |> withSchema companyEmailSchema
    |> field "phone" _.Phone
    |> construct Signup.create
```

Before the field cursor is finalized, run a Fantomas spike over flat, parenthesized, and lambda-based configurations.
All user-facing examples must survive automatic formatting without manual indentation.

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

### 9. Introduce The Typed Current-Field Cursor

Delay default-schema resolution until a field is committed. Add `withSchema`. Support inferred fields, explicit
matching schemas, inferred raw schemas followed by refinement, configured raw schemas followed by refinement, and
value-preserving validation.

Add compile-time tests proving that an incomplete raw-to-model field cannot be committed.

### 10. Migrate Schema Declarations And Remove `fieldWith`

Update hand-written schemas, generated contracts, generator output, examples, tests, benchmarks, and reference
applications. Run Fantomas over the migrated declarations. Remove `fieldWith` once no source uses it.

### 11. Add Field-Level `refine` And `validate`

Make the current-field cursor apply the same refinement definition as `Schema.refine`. Make field-level `validate`
apply to the current completed value schema. Test operation ordering across raw constraints, refinement, and refined
value validation.

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
let email: Email =
    Refine.from rawEmail
```

After:

```fsharp
let email: Email =
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
Schema.define<Signup>
|> field "email" _.Email
|> field "phone" _.Phone
|> construct Signup.create
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
Schema.define<Node>
|> field "children" _.Children
|> withSchema (Schema.listWith nodeSchema)
|> construct Node.create
```

Main benefit: declaring the field and configuring its schema use separate, regularly composable operations.

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
Schema.define<Signup>
|> field "email" _.Email
|> refine Email.refinement
|> construct Signup.create
```

Main benefit: `Refinement<string, Email>` supplies the raw type, allowing Schema to infer `Schema.text`.

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
Schema.define<Signup>
|> field "email" _.Email
|> withSchema Schema.text
|> constrain required
|> refine Email.refinement
|> construct Signup.create
```

Main benefit: pipeline order shows that `required` applies to raw text before construction of `Email`.

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
Schema.define<Signup>
|> field "email" _.Email
|> construct Signup.create

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
    Schema.define<Signup>
    |> field "email" _.Email
    |> field "phone" _.Phone
    |> construct Signup.create

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
