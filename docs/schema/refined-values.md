# Refined schemas

This page shows how a fallible refined-value constructor becomes an ordinary `Schema<'value>`.

`Axial.Refined` owns parsing and smart construction. `Axial.Schema` owns boundary shape, metadata, paths, and
interpreters. `Schema.refine` connects them without making either package depend in the opposite direction.

```fsharp
type Email = private Email of string

module Email =
    let create raw =
        if System.Net.Mail.MailAddress.TryCreate raw |> fst then Ok(Email raw)
        else Error "email.invalid"

    let value (Email raw) = raw

    let schema : Schema<Email> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.email ]
        |> Schema.refine
            create
            (fun code -> [ SchemaError.Custom(code, None) ])
            value
        |> Schema.withFormat SchemaFormat.email
```

The arguments are:

```fsharp
Schema.refine
    (construct : 'raw -> Result<'value, 'error>)
    (mapError : 'error -> SchemaError list)
    (inspect : 'value -> 'raw)
    (raw : Schema<'raw>)
```

Construction interpreters call `construct`. Inspection, encoding, generation, and checking use `inspect`. Both
directions are required because a schema is more than an input parser.

`Schema.convert` remains appropriate for total, information-preserving domain conversions:

```fsharp
type UserId = private UserId of Guid

let userIdSchema =
    Schema.guid |> Schema.convert UserId (fun (UserId value) -> value)
```

Do not hide a fallible constructor behind `failwith` or an internal unchecked constructor merely to use `convert`.
Use `refine` and preserve the error path.

## The built-in catalog

`RefinedSchemas` is a sibling catalog whose members all return `Schema<_>`:

```fsharp
RefinedSchemas.nonBlankString
RefinedSchemas.boundedString 2 80
RefinedSchemas.trimmedString
RefinedSchemas.slug
RefinedSchemas.positiveInt
RefinedSchemas.nonNegativeInt
RefinedSchemas.nonZeroInt
RefinedSchemas.negativeInt
RefinedSchemas.nonPositiveInt
RefinedSchemas.nonEmptyList RefinedSchemas.slug
RefinedSchemas.distinctList Schema.text
RefinedSchemas.boundedList 1 10 Schema.guid
```

`RefinedSchemas.dateTimeOffsetRange` and, on supported targets, `dateOnlyRange` are record schemas whose constructors
enforce range ordering.

## Where constraints belong

Raw constraints describe the boundary representation and provide portable metadata:

```fsharp
Schema.text
|> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]
|> Schema.refine WorkspaceName.create SchemaError.ofRefinementError WorkspaceName.value
```

The smart constructor describes the intrinsic domain invariant. Repeating an intrinsic rule as a raw constraint is
useful when it supplies standard error codes, HTML attributes, JSON Schema output, or generators. The constructor
must still reject invalid input independently; otherwise direct construction and schema construction disagree.

Constraints added after refinement describe the refined value and run through its inspected representation. Use
`Schema.rawConstraints`, `Schema.constraints`, `Schema.underlyingPrimitiveKind`, and `Schema.inspectUnderlying` when
writing an interpreter that needs to distinguish those layers.

## Nested and collection refinements

Because every member returns `Schema<_>`, composition needs no adapter:

```fsharp
let tags =
    RefinedSchemas.nonEmptyList RefinedSchemas.slug

let request =
    Schema.recordFor<Request, _> (fun name tags -> { Name = name; Tags = tags })
    |> Schema.field "name" _.Name RefinedSchemas.nonBlankString
    |> Schema.field "tags" _.Tags tags
    |> Schema.build
```

A refinement can also wrap a record, union, list, option, or map schema. Its constructor receives the successfully
parsed raw value; its inspection function returns that representation for other interpreters.
