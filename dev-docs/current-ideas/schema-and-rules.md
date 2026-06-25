# Schema And Rules Ideas

Status: pre-idea.

The idea is to grow beyond immediate `Check` calls into reusable, path-aware validation programs.

## Rule Layer

`Check` validates one value now. A future `Rule` or `ModelRule` layer would validate a model later, with reusable field
rules and nested path information:

```fsharp
ModelRule.define [
    field "Username" _.Username [
        whenNotBlank UsernameRequired
        whenMinLength 3 UsernameTooShort
    ]
    nested "Address" _.Address validateAddress
    each "Lines" _.Lines (fun i ->
        field "Name" _.Name [ whenNotBlank (LineNameRequired i) ]
    )
]
```

Prefer explicit field descriptors as the baseline because they are AOT-safe, reflection-free, and easy for agents to
reason about. Quotations may help capture paths for pure validation, but they do not solve setters for parse/schema
work.

## Parse Layer

Parsing is different from checking: it turns an untrusted input type into a trusted output type.

```fsharp
type Parse<'raw, 'model, 'error> = 'raw -> Validation<'model, 'error>
```

This should not be treated as a small extension of `Rule<'root, 'error>`. Each field can produce a different output
type, and those outputs feed a constructor.

## Schema Layer

If Axial and CodecMapper share structural definitions, the common primitive is a schema/field description with:

- field name/path
- getter
- setter
- value schema

For AOT and trimming safety, avoid runtime reflection as the foundation. Use explicit definitions first; consider Myriad
or another F# build-time generator only after the API shape stabilizes.
