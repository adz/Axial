# Schema Computation Expression Evaluation

Phase 15 asked whether a `schema create { ... }` computation expression beats the progressive pipeline builder on
readability and compile-error quality. This note records the experiment and the outcome: **do not ship the CE.**

## Experiment

A minimal CE was prototyped over the existing `SchemaBuilder` core (2026-07-05, F# 9 / .NET 10):

```fsharp
let personSchema =
    schemaFor<Person> {
        record (fun name age -> { Name = name; Age = age })
        textWith "name" _.Name (constraints { required; maxLength 80 })
        int "age" _.Age
    }
```

- Custom operations (`record`, `text`, `int`, generic `field`) thread the progressive `SchemaBuilder` state through the
  CE, so the typed constructor-peeling core is fully reusable — the CE is genuinely just sugar.
- Constraint blocks work as a second small CE (`constraints { required; maxLength 80 }`).

## Findings

1. **The sketched syntax is not achievable.** `text "name" _.Name { required; maxLength 80 }` with bare braces cannot
   parse as a constraint block argument; F# only treats `{ ... }` as a computation expression after a builder-valued
   expression. The real syntax is `text "name" _.Name (constraints { ... })`, which is no shorter than the pipeline's
   `Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.maxLength 80 ] "name" _.Name Value.text`.
2. **Compile-error quality is a wash.** For a mis-typed getter the CE reports a well-localized
   `expected 'string' but here has type 'int'` at the getter, which is *better* than the pipeline's `SchemaBuilder<...>`
   type dump. For a missing field the CE reports `'Person' does not match 'int -> Person'` at the whole expression,
   which is comparable to the pipeline. Neither side wins across the board.
3. **Readability does not improve.** The CE removes `|>` and `Schema.` prefixes but adds a `record` operation line, a
   builder value per model type (`schemaFor<Person>`), and a second authoring vocabulary that duplicates every pipeline
   operation. Primitive operations named `int`/`bool` also shadow F# core functions inside the CE body.

## Outcome

The phase's own bar was "ship it only if it beats the pipeline on readability and compile-error quality for constraint
blocks." It does not: constraint blocks are where the sketch most clearly fails (finding 1). The pipeline remains the
single public authoring surface; external-name-first ordering is unaffected. Rails ActiveModel comparisons belong to the
guide work in Phase 17 using the pipeline surface.

If a future F# version makes trailing builder-block arguments expressible, re-run this evaluation before reopening the
design.
