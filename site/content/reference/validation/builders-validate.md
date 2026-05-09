---
title: validate
type: docs
---

The accumulating `validate { }` computation expression.


```fsharp
let validate
```


## Remarks

<para>
Use this builder when you want to collect all validation failures instead of stopping
at the first one.
</para>
<para>
Use `and!` when sibling validations should accumulate into the same diagnostics graph.
Plain `let!` and `do!` are sequential: if the left side fails, the later step is
not evaluated.
</para>
<para>
`Check<'value>` covers both value-preserving checks and gate checks.
Use `Check.orError` to attach an application error, and `Guard.Of` /
`Guard.MapError` when you want the same error-bound source shape to participate
directly in validation.
</para>
<para>
When nested API response fields need to keep their place in the diagnostics graph, use
the scoped helpers `validate.key`, `validate.index`, and `validate.name`
inside the computation expression. If you already have a `Validation` value, use
`Validation.key`, `Validation.index`, or `Validation.name` to prefix it
after the fact.
</para>
<para>
It is intended for forms, configuration checks, and other input-heavy boundaries where
the user benefits from seeing every problem at once.
</para>


## Information

- **Module**: `Builders`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Builders.fs#L743)

## Examples

```fsharp
let validatedUser =
    validate {
        let! name = Check.notBlank input.Name
        let! age = Check.okIf (input.Age > 0) "Age must be positive"
        return { Name = name; Age = age }
    }
```

```fsharp
let validatedCustomer =
    validate.key "customer" {
        let! name =
            validate.name "Name" {
                return! input.Name |> Check.notBlank |> Check.orError "Name required"
            }

        return name
    }
```

