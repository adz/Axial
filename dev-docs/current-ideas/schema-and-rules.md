# Schema, Input, Check, Rules, And Policy Direction

Status: consolidated pre-idea.

This document is the single current sketch for Axial's data-boundary direction. It combines the schema/input/rules
direction with the useful `Policy` idea from the older error-handling/refined/policy PRD.

## Goal

Axial should be as lightweight and declarative as Rails ActiveModel for common model boundaries, while being better in
the ways F# should be better:

- models are trusted by construction
- raw input and parsed models are separate
- errors are typed and path-aware
- failed input retains raw values for redisplay
- refined/domain types remain first-class
- schema metadata can drive validation, input parsing, codecs, JSON Schema, docs, and UI
- contextual workflow requirements compose with `Flow`

Rails gets the ergonomics right:

```ruby
class ContactForm
  include ActiveModel::Model

  attribute :name, :string
  attribute :email, :string
  attribute :message, :string

  validates :name, presence: true, length: { maximum: 20 }
  validates :email, presence: true, format: { with: URI::MailTo::EMAIL_REGEXP }
  validates :message, presence: true, length: { minimum: 10 }
end
```

Axial should keep the compact field declaration, default errors, field error lookup, and raw redisplay story. It should
not copy the mutable "maybe valid" model object.

## Concepts

The cohesive model:

```text
Check describes reusable value constraints.
Schema describes model structure and construction.
Input parses raw data with schemas.
Validation interprets schemas against existing models.
Rules describe contextual requirements over trusted models.
Policy adapts checks/rules/parsers into Flow.
```

Primary pipeline:

```text
RawInput
    -> Input.parse Schema
    -> trusted model
    -> Rules.apply context
    -> Flow.verify Policy when workflow integration is needed
```

Schema is broader than validation:

```text
Schema<'model>
    -> Input interpreter
    -> Validation interpreter
    -> Codec interpreter
    -> JSON Schema interpreter
    -> UI/documentation interpreters
```

## User Shape

Preferred handwritten schema:

```fsharp
type Contact =
    private
        {
            Name: string
            Email: Email
            Message: string
        }

module Contact =
    let private create name email message =
        {
            Name = name
            Email = email
            Message = message
        }

    let schema =
        schema create {
            text "name" _.Name {
                required
                maxLength 20
            }

            field "email" _.Email Email.schema {
                required
            }

            text "message" _.Message {
                required
                minLength 10
            }
        }
```

Each field declaration carries:

- external name, such as `"name"`
- getter, such as `_.Name`
- value schema, such as `text` or `Email.schema`
- intrinsic construction constraints, such as `required` or `maxLength 20`
- constructor position through the enclosing `schema create { ... }`

Primitive shortcuts keep the common case close to ActiveModel:

```fsharp
text "name" _.Name { required }
int "age" _.Age { min 0 }
date "publishedOn" _.PublishedOn { optional }
field "email" _.Email Email.schema { required }
nested "address" _.Address Address.schema { required }
many "contacts" _.Contacts ContactMethod.schema { minCount 1 }
```

## Schema Constraints Lower To Checks And Metadata

Schema constraints are declarative metadata first. They are richer than executable validators.

This:

```fsharp
text "name" _.Name {
    required
    maxLength 20
}
```

records semantic constraints on the `name` field. Different interpreters lower those constraints differently.

For `required`:

```text
Input parser      -> reject missing raw fields and missing/blank scalar values
Diagnostics       -> SchemaError.Required at path name
JSON Schema       -> add name to required fields
UI interpreter    -> mark the control required
Docs interpreter  -> document the field as required
```

For `maxLength 20`:

```text
Runtime check     -> Check.String.maxLength 20
Diagnostics       -> SchemaError.TooLong 20 at path name
JSON Schema       -> maxLength: 20
UI interpreter    -> maxlength="20"
Docs interpreter  -> document maximum length 20
```

So this:

```fsharp
maxLength 20
```

is richer than this:

```fsharp
check (Check.String.maxLength 20)
```

Use named schema constraints when the concept is portable to diagnostics, JSON Schema, UI, docs, or another interpreter.
Use explicit `Check.*` pipelines for custom logic or reusable constraint programs.

## Check

`Check` is a complete Axial subsystem for typed, composable value constraints. It is not the whole product, but it
should be complete enough to stand on its own.

Candidate shape:

```fsharp
type Check<'value> = 'value -> Result<unit, CheckFailure list>

type CheckFailure =
    | Missing
    | Blank
    | InvalidFormat of expected: string
    | TooShort of minimum: int
    | TooLong of maximum: int
    | OutOfRange of minimum: string * maximum: string
```

Top-level `Check` owns composition:

```fsharp
Check.all [
    Check.String.present
    Check.String.maxLength 20
]

Check.all [
    Check.Number.between 1 10
]

Check.any [
    Check.String.email
    Check.String.matches phonePattern
]
```

Typed modules own typed vocabularies:

```fsharp
module Check =
    val all: Check<'value> list -> Check<'value>
    val any: Check<'value> list -> Check<'value>
    val not: Check<'value> -> Check<'value>
    val mapFailure: (CheckFailure -> CheckFailure) -> Check<'value> -> Check<'value>

module Check.String =
    val present: Check<string>
    val minLength: int -> Check<string>
    val maxLength: int -> Check<string>
    val lengthBetween: int -> int -> Check<string>
    val email: Check<string>
    val matches: string -> Check<string>
    val oneOf: string list -> Check<string>

module Check.Number =
    val between: minimum: 'n -> maximum: 'n -> Check<'n> when 'n: comparison
    val greaterThan: minimum: 'n -> Check<'n> when 'n: comparison
    val lessThan: maximum: 'n -> Check<'n> when 'n: comparison
    val atLeast: minimum: 'n -> Check<'n> when 'n: comparison
    val atMost: maximum: 'n -> Check<'n> when 'n: comparison

module Check.Collection =
    val notEmpty: Check<#seq<'value>>
    val minCount: int -> Check<#seq<'value>>
    val maxCount: int -> Check<#seq<'value>>
    val countBetween: int -> int -> Check<#seq<'value>>
    val distinct: Check<#seq<'value>> when 'value: equality

module Check.Option =
    val some: Check<'value option>
    val none: Check<'value option>

module Check.Result =
    val ok: Check<Result<'value, 'error>>
    val error: Check<Result<'value, 'error>>
```

`Check` is intentionally path-free and raw-input-free. It does not know about models, diagnostics trees, localization, or
effects. Schema, input, validation, rules, and policy give checks context.

Important distinction:

- `required` is schema/input-level because it understands missing raw fields and optionality.
- `Check.String.present`, `Check.Option.some`, and `Check.Collection.notEmpty` are value-level checks over already parsed
  values.

## Refined Types

Refined/domain types are first-class value schemas. They are not replaced by schema constraints.

Use a refined type when a concept has reusable domain meaning:

```fsharp
type Email = private Email of string

module Email =
    let private create text =
        Email text

    let value (Email text) =
        text

    let schema =
        refined create value text {
            check [
                Check.String.present
                Check.String.email
            ]

            format "email"
        }
```

Then model schemas use it:

```fsharp
field "email" _.Email Email.schema {
    required
}
```

Use local schema constraints when the rule only belongs to one model:

```fsharp
text "name" _.Name {
    required
    maxLength 20
}
```

Do not create a new wrapper just because a field gained a second constraint. The decision is semantic:

- reusable domain concept: define a refined/domain type with a schema
- local construction invariant: keep it on the model schema field

## Input

Input is the interpreter that turns untrusted raw data into trusted models.

Raw input is source-agnostic and tree-shaped:

```fsharp
type RawInput =
    | Missing
    | Scalar of string
    | Many of RawInput list
    | Object of Map<string, RawInput>
```

Edge adapters convert host-specific data:

```fsharp
RawInput.ofMap fields
RawInput.ofNameValueCollection request.Form
RawInput.ofArgs argv
RawInput.ofJson json
RawInput.ofConfiguration configuration
```

Parsing:

```fsharp
let parsed = Input.parse Contact.schema rawContact

parsed.IsValid
parsed.Input "email"
parsed.ErrorsFor "email"
parsed.Result
```

Result shape:

```fsharp
type ParsedInput<'model, 'error> =
    {
        Raw: RawInput
        Result: Result<'model, Diagnostics<'error>>
    }

    member IsValid: bool
    member Model: 'model
    member TryModel: 'model option
    member Errors: Diagnostics<'error>
    member ErrorsFor: path: string -> 'error list
    member Input: path: string -> string
```

The invalid thing is `ParsedInput`, not `Contact`.

Input parse mechanics for:

```fsharp
text "name" _.Name {
    required
    maxLength 20
}
```

are:

1. read `RawInput` at path `name`
2. apply raw-shape requirements such as missing/required
3. parse `RawInput.Scalar text` as a string
4. run value checks such as `Check.String.maxLength 20`
5. attach failures at `PathSegment.Name "name"`
6. repeat sibling fields applicatively so all field errors accumulate
7. call `create name email message` only if all intrinsic construction requirements pass

Raw redisplay:

```fsharp
let parsed = Input.parse Contact.schema rawContact

if parsed.IsValid then
    save parsed.Model
else
    renderField "name" (parsed.Input "name") (parsed.ErrorsFor "name")
    renderField "email" (parsed.Input "email") (parsed.ErrorsFor "email")
    renderField "message" (parsed.Input "message") (parsed.ErrorsFor "message")
```

## Default Errors

Users should not have to define application errors for ordinary validation. Axial owns default typed schema/input errors.

```fsharp
type SchemaError =
    | Required
    | ExpectedScalar
    | ExpectedObject
    | ExpectedMany
    | InvalidFormat of expected: string
    | TooShort of minimum: int
    | TooLong of maximum: int
    | OutOfRange of minimum: string * maximum: string
    | Custom of code: string * message: string
```

Diagnostics carry the path, so `SchemaError.Required` does not need to store the field name.

Override messages only when needed:

```fsharp
field "email" _.Email Email.schema {
    required
    message "Enter a valid email address."
}
```

Map to domain errors when needed:

```fsharp
field "email" _.Email Email.schema {
    required
    mapError ContactError.Email
}
```

## Rules

Rules are contextual checks over already-trusted models. They are not the model construction contract.

```fsharp
let supportTicket =
    rules<Contact> {
        require "message" (fun contact -> contact.Message.Length >= 50) {
            code "message.too_short_for_support"
            message "Support requests need at least 50 characters."
        }
    }
```

Use after input parsing:

```fsharp
match Input.parse Contact.schema rawContact |> _.Result with
| Error diagnostics ->
    showRawInputErrors diagnostics

| Ok contact ->
    match Rules.apply supportTicket contact with
    | Ok contact ->
        save contact
    | Error diagnostics ->
        showWorkflowErrors contact diagnostics
```

Schema answers: can this model be constructed?

Rules answer: is this trusted model acceptable in this workflow?

## Policy

`Policy` is the Flow integration layer for requirements. It adapts checks, parsers, refined constructors, input results,
validation results, or rules into the error channel of a workflow.

Core shape:

```fsharp
type Policy<'env, 'error, 'input, 'output> =
    'env -> 'input -> Result<'output, 'error>
```

Meaning:

```text
given workflow environment
and an input value
either produce allowed/transformed output
or fail with workflow error
```

`Policy` lives with `Axial.Flow` because it can read workflow environment. It should not force `Axial.Flow` to depend on
`Axial.Schema`, `Axial.Refined`, or `Axial.Validation`; policy constructors adapt ordinary `Result`-returning functions.

Expected API:

```fsharp
module Policy =
    val pure:
        ('input -> Result<'output, 'innerError>) ->
        ('innerError -> 'error) ->
        Policy<'env, 'error, 'input, 'output>

    val withError:
        ('input -> Result<'output, 'innerError>) ->
        'error ->
        Policy<'env, 'error, 'input, 'output>

    val context:
        ('env -> 'input -> Result<'output, 'innerError>) ->
        ('innerError -> 'error) ->
        Policy<'env, 'error, 'input, 'output>

    val pass:
        Policy<'env, 'error, 'input, 'input>

    val compose:
        Policy<'env, 'error, 'input, 'middle> ->
        Policy<'env, 'error, 'middle, 'output> ->
        Policy<'env, 'error, 'input, 'output>

    val optional:
        ('env -> bool) ->
        Policy<'env, 'error, 'input, 'input> ->
        Policy<'env, 'error, 'input, 'input>

module Flow =
    val verify:
        Policy<'env, 'error, 'input, 'output> ->
        'input ->
        Flow<'env, 'error, 'output>
```

Examples:

```fsharp
module Policies =
    let parseCount =
        Policy.withError Parse.int AppError.BadCountFormat

    let parseEmail =
        Policy.pure Email.parse AppError.InvalidEmail

    let supportTicket =
        Policy.pure (Rules.apply ContactRules.supportTicket) AppError.InvalidSupportTicket

    let belowTenantLimit =
        Policy.context
            (fun env amount ->
                if amount <= env.Tenant.MaxOrderAmount then Ok amount
                else Error AmountTooLarge)
            AppError.Policy
```

Use:

```fsharp
let process raw = flow {
    let! contact =
        raw
        |> Input.parse Contact.schema
        |> _.Result

    let! contact =
        contact |> Flow.verify Policies.supportTicket

    return contact
}
```

`Policy` is not a replacement for `Bind.error` or `Bind.mapError`. `Bind` remains a bind-site adapter for immediate
assignment or mapping. `Policy` is a named, reusable environment-aware requirement.

## Nested Models

Nested models are schemas inside schemas.

```fsharp
type Address =
    private
        {
            Street: string
            Postcode: string
        }

module Address =
    let private create street postcode =
        {
            Street = street
            Postcode = postcode
        }

    let schema =
        schema create {
            text "street" _.Street {
                required
            }

            text "postcode" _.Postcode {
                required
                pattern @"^\d{4}$"
            }
        }

type Customer =
    private
        {
            Name: string
            Address: Address
        }

module Customer =
    let private create name address =
        {
            Name = name
            Address = address
        }

    let schema =
        schema create {
            text "name" _.Name {
                required
            }

            nested "address" _.Address Address.schema {
                required
            }
        }
```

`nested "address" _.Address Address.schema`:

1. gives non-input interpreters a getter for `Address`
2. reads raw child input at path `address` for input parsing
3. requires object-shaped raw input unless optional
4. parses the child with `Address.schema`
5. prefixes diagnostics with `PathSegment.Name "address"`

Example path:

```text
address.postcode:
  - InvalidFormat "pattern"
```

## Collections

Collections parse every item and accumulate every item error.

```fsharp
type ContactMethod =
    private
        {
            Kind: string
            Value: string
        }

module ContactMethod =
    let private create kind value =
        {
            Kind = kind
            Value = value
        }

    let schema =
        schema create {
            text "kind" _.Kind {
                required
                oneOf [ "email"; "phone" ]
            }

            text "value" _.Value {
                required
            }
        }

type Customer =
    private
        {
            Name: string
            Contacts: ContactMethod list
        }

module Customer =
    let private create name contacts =
        {
            Name = name
            Contacts = contacts
        }

    let schema =
        schema create {
            text "name" _.Name {
                required
            }

            many "contacts" _.Contacts ContactMethod.schema {
                minCount 1
            }
        }
```

Example path:

```text
contacts[1].value:
  - Required
```

Raw redisplay uses the same paths:

```fsharp
parsed.Input "contacts[1].value"
```

## Constructor Errors

Some intrinsic invariants involve multiple fields. The constructor should be allowed to return `Result`.

```fsharp
type DateRange =
    private
        {
            Start: DateOnly
            End: DateOnly
        }

module DateRange =
    let private create startDate endDate =
        if endDate < startDate then
            Error DateRangeError.EndBeforeStart
        else
            Ok {
                Start = startDate
                End = endDate
            }

    let schema =
        schemaResult create {
            date "start" _.Start {
                required
            }

            date "end" _.End {
                required
            }
        }
        |> Input.constructorErrorAt "end"
```

System-wide construction invariants stay in construction. Workflow-specific constraints stay in rules.

## Validation, Codec, Docs, And UI Interpreters

Validation against existing models uses getters, not raw input:

```fsharp
Validation.validate Contact.schema contact
```

Because schema has getters and value schemas, non-validation interpreters can also consume it:

```fsharp
Codec.encode Contact.schema contact
Codec.decode Contact.schema json
JsonSchema.generate Contact.schema
Docs.describe Contact.schema
Ui.describe Contact.schema
```

The same declaration:

```fsharp
field "email" _.Email Email.schema {
    required
}
```

can mean:

- input parser: require and parse an email from raw input
- validator: check an existing email value
- codec: encode/decode the email value
- JSON Schema: emit `{ "type": "string", "format": "email" }`
- UI: render an email input with required metadata

## Implementation Shape

The polished DSL should be sugar over explicit core operations.

Core types:

```fsharp
type Check<'value> = 'value -> Result<unit, CheckFailure list>
type Schema<'model>
type ValueSchema<'value>
type Field<'model, 'value>
type RuleSet<'model, 'error>

type RawInput =
    | Missing
    | Scalar of string
    | Many of RawInput list
    | Object of Map<string, RawInput>

type ParsedInput<'model, 'error> =
    {
        Raw: RawInput
        Result: Result<'model, Diagnostics<'error>>
    }

type Policy<'env, 'error, 'input, 'output> =
    'env -> 'input -> Result<'output, 'error>
```

Core modules:

```fsharp
module Schema =
    val map2:
        constructor: ('a -> 'b -> 'model) ->
        Field<'model, 'a> ->
        Field<'model, 'b> ->
        Schema<'model>

    val field:
        externalName: string ->
        getter: ('model -> 'value) ->
        value: ValueSchema<'value> ->
        Field<'model, 'value>

module Value =
    val text: ValueSchema<string>
    val int: ValueSchema<int>
    val date: ValueSchema<DateOnly>
    val required: ValueSchema<'value> -> ValueSchema<'value>
    val optional: ValueSchema<'value> -> ValueSchema<'value option>
    val maxLength: int -> ValueSchema<string> -> ValueSchema<string>
    val minLength: int -> ValueSchema<string> -> ValueSchema<string>
    val email: ValueSchema<string> -> ValueSchema<string>
    val refined:
        construct: ('raw -> 'value) ->
        inspect: ('value -> 'raw) ->
        raw: ValueSchema<'raw> ->
        ValueSchema<'value>

module Input =
    val parse:
        Schema<'model> ->
        RawInput ->
        ParsedInput<'model, SchemaError>

module Rules =
    val apply:
        RuleSet<'model, 'error> ->
        'model ->
        Result<'model, Diagnostics<'error>>
```

If computation expression syntax is hard to implement first, start explicit:

```fsharp
let schema =
    Schema.map3 create
        (Schema.field "name" _.Name (Value.text |> Value.required |> Value.maxLength 20))
        (Schema.field "email" _.Email (Email.schema |> Value.required))
        (Schema.field "message" _.Message (Value.text |> Value.required |> Value.minLength 10))
```

Then add the DSL.

## Current Codebase Recommendations

### 1. Promote Check Into A Real Subsystem

Current repository instructions and sibling docs say `Check` is pure predicates returning `bool`. Replace that direction
with:

```text
Check = complete typed value-constraint subsystem
Check<'value> = 'value -> Result<unit, CheckFailure list>
```

Update `AGENTS.md`, `dev-docs/decisions/README.md`, `dev-docs/project-split.md`, and stale current-ideas notes before
source changes.

### 2. Keep Schema Separate From Flow

Schema, input parsing, validation, and rules should not depend on `Axial.Flow`. `Policy` lives in `Axial.Flow` as the
adapter layer.

### 3. Keep Diagnostics In Axial.Validation

`Validation<'value, 'error>` and `Diagnostics<'error>` remain in `Axial.Validation`. Schema describes; interpreters
produce diagnostics.

### 4. Package Direction

Long-term packages:

```text
Axial.ErrorHandling
    Check<'value>
    CheckFailure
    Typed Check modules
    Result helpers

Axial.Schema
    Schema<'model>
    ValueSchema<'value>
    Field metadata
    Construction and inspection descriptors
    Schema constraint metadata

Axial.Validation
    Validation<'value, 'error>
    Diagnostics<'error>

Axial.Validation.Schema
    Input.parse
    Validation.validate
    Rules.apply
    SchemaError / diagnostic interpretation

Axial.Refined
    Refined/domain value types
    Smart constructors
    Value schemas for refined types

Axial.Flow
    Flow
    Policy
    Flow.verify
```

### 5. Implement Explicit Core Before DSL

First prove:

- complete typed `Check` composition
- constructor application
- getter metadata
- field ordering
- raw tree pathing
- diagnostics pathing
- nested schemas
- collections
- constructor errors
- refined value schemas
- default errors
- policy adaptation into `Flow`

Then add computation expression syntax.

### 6. Source Generation Later

Runtime reflection should not be the foundation. A source generator can later remove constructor/getter repetition:

```fsharp
[<Schema>]
type Contact =
    {
        [<Required; MaxLength 20>]
        Name: string

        [<Required; Email>]
        Email: Email

        [<Required; MinLength 10>]
        Message: string
    }
```

Generation is convenience, not the core design.

## Important Tensions

### Getter And Constructor Alignment

The schema needs both read and write directions:

- getter: inspect existing models
- constructor slot: build new models

F# cannot automatically prove that `_.Name` corresponds to the first `create` argument. Keep the handwritten API simple,
test field order heavily, and consider generation after the shape stabilizes.

### Schema Constraints Vs Checks

Schema constraints carry metadata and lower to checks. Checks are executable value programs. Do not collapse the two.

### Schema Constraints Vs Refined Types

Local rules belong on schema fields. Reusable semantic concepts belong in refined/domain types.

### Required Vs Present

`required` handles raw missing values and optionality. Typed checks such as `Check.String.present`,
`Check.Option.some`, and `Check.Collection.notEmpty` handle already parsed values.

### Rules Vs Policy

Rules are contextual checks over trusted models. Policy is the `Flow` adapter for any reusable requirement, including
rules.

## Non-Negotiables

- Schema is broader than validation.
- Input parsing is an interpreter over schema.
- Validation is an interpreter over schema.
- Rules are contextual and post-construction.
- Policy adapts reusable requirements into `Flow`.
- Models constructed from untrusted data must go through schema/input parsing.
- Raw input must be retained after failed parse for redisplay.
- Refined types remain first-class value schemas.
- `Check` is complete enough to be a real subsystem, not a partial helper catalog.
- `Check` remains path-free and raw-input-free.
- Default typed errors exist for common validation.
- Runtime reflection is not the foundation.
- Source generation can remove ceremony later, but explicit definitions must work first.
