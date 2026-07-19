# Constructor-last schema authoring: design

Status: sole public authoring surface (`Shape.fs`, 2026-07-19). Handwritten schemas, schemagen output, tests, examples, and codecs use it.

## Target

```fsharp
let personSchema =
    Schema.define<Person>
    |> field "firstName" _.FirstName
    |> constrain (minLength 1)
    |> field "lastName" _.LastName
    |> constrain (minLength 1)
    |> field "birthDate" _.BirthDate
    |> constructResult Person.Create
```

Constructor last. Constraints next to their field. Primitive schemas inferred. No repeated type
information. No `build`.

## The important separation

`Schema.define<Person>` cannot be a `Schema<Person>` — it doesn't know how to construct a `Person`.
It is an `ObjectShape<Person, NoFields>`:

```
define + fields         = structural shape      (ObjectShape<'model,'fields>)
structural shape + ctor = schema                (construct / constructResult)
```

This separation is what generated drafts need too: a draft schema is a structural shape closed with the
draft's own record constructor; the domain schema is the same shape admitted through a checked
constructor.

## How each piece works

**Phantom accumulation.** `'fields` grows one tuple layer per field: after `string`, `string`,
`DateTimeOffset` it is `((NoFields * string) * string) * DateTimeOffset`. The shape's runtime state is
just boxed `FieldDefinition<'model,_>` values, newest first; the phantom lets every operation recover
them at their real types with `unbox` — no reflection.

**`constrain` binds to the current field.** Its signature demands the phantom's last entry:
`Constraint<'v> -> ObjectShape<'m, 'rest * 'v> -> ObjectShape<'m, 'rest * 'v>`. Adding another field
commits the previous one; `construct` commits the last. `constrain (minLength 1)` after an `int` field
is a type mismatch on the `constrain` line.

**Typed constraints.** `Constraint<'value>` wraps the untyped `Constraint` with a phantom value type.
The vocabulary lives in `Syntax`: text constraints are `Constraint<string>`, list constraints
`Constraint<'item list>`, comparisons generic. The untyped `Constraint` stays as interpreter metadata and for value-schema composition.

**Inference.** `field` resolves `Schema<'v>` from the getter's result type through
`SchemaDefaults.Resolve()` — SRTP over an overload set (primitives, options, and recursively resolved lists and maps), with a
hook for any type exposing `static member Schema: T -> Schema<T>` (generated types will use
this). No match → compile error → `fieldWith explicitSchema`.

**Constructor-last application.** F# has no variadic application, and curried function types overlap
(`'a -> 'm` matches any longer signature), so overloads on the constructor argument alone are ambiguous.
The shape's phantom is not ambiguous. `construct`/`constructResult` are inline SRTP dispatchers passing
*both* the constructor and the shape to an overload set (`Constructors`, one member per arity, 1–12).
The overload closes erased model metadata and a retained typed record plan. Chosen over an HList encoding deliberately: the arity members are
boring, inspectable, and produce concrete-type errors at the closing call.

**Checked construction.** `constructResult` takes `... -> Result<'model, string>`. Parsing runs the
constructor only after all fields pass, and a rejection becomes `SchemaError.ConstructorFailed`, placed
by the parser's `constructorErrorAt` option.

**`Schema.admit`** is the trusted-construction primitive:

```fsharp
Schema.admit : ('draft -> Result<'domain, string>) -> ('domain -> 'draft) -> Schema<'draft> -> Schema<'domain>
```

It rewrites the draft's model definition in place: getters compose with the projection, the constructor
composes with the admission function. Field shape, wire names, constraints, docs, parsing, checking, and
JSON Schema output are all preserved. The projection is required because a schema reads values back out
(serialization, redisplay, checking) — admission alone is one-directional.

The manual draft pattern, which generation will later emit:

```fsharp
let bookingDraftSchema =
    Schema.define<BookingDraft>
    |> field "start" _.Start
    |> field "end" _.End
    |> construct BookingDraft.Create

let bookingSchema =
    bookingDraftSchema |> Schema.admit Booking.Create _.ToDraft
```

## Known trades

- **Positional matching.** The constructor's parameters must match field order and types. Two adjacent
  same-typed fields swapped against the constructor compile and run wrong. A constructor taking a draft record is immune because its record literal names every field.
  Accepted; documented.
- **Error locality.** Field/constraint mistakes are caught on their own line; arity and type mismatches
  against the constructor are caught at `construct`, not per field.
- **`SchemaDefaults` is a closed overload set plus an opt-in member.** Deliberate: no silent global
  registry, no reflection fallback.

## Not done yet, in order

1. **Admission diagnostics.** `admit`/`constructResult` take rendered `string` errors. Domain errors can be mapped inside the admission function. Consider structured, multi-path constructor diagnostics; today a rejection lands at one path.
2. **Admission specialization.** `constructResult` retains the same typed record plan as `construct`. `Schema.admit` still rewrites erased metadata and lacks a typed plan.
3. **Domain generation.** Emit `Person.Schema` and generated field definitions, then add draft generation when the transformation developer experience is proven.
