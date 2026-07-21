---
weight: 20
title: vs FluentValidation
description: Validators check existing objects; Axial schemas never construct the invalid object.
---

# Axial vs FluentValidation

This page compares Axial's schema-first parsing with FluentValidation's validator classes for readers deciding between
the two models.

The short version: FluentValidation validates objects that already exist. Axial parses input into objects that cannot
exist in an invalid state. That difference decides everything else.

## The Model Difference

A FluentValidation validator receives a constructed object and reports rule failures:

```csharp
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Age).InclusiveBetween(13, 120);
    }
}
```

The `Customer` had to be constructed first — usually by a model binder filling public setters with whatever arrived.
Between construction and validation (and anywhere a code path forgets to call the validator) an invalid `Customer`
exists and can leak.

An Axial schema owns construction. Parsing either produces a trusted model or path-aware diagnostics; there is no
intermediate invalid object:

```fsharp
open Axial.Schema.Syntax
let customerSchema =
    Schema.define<Customer>
    |> field "name" _.Name
    |> constrain (maxLength 80)
    |> field "age" _.Age
    |> constrain (between 13 120)
    |> construct (fun name age -> { Name = name; Age = age })

match (Schema.parse customerSchema raw) with
| Ok customer -> customer          // every Customer in the program passed the boundary
| Error diagnostics -> reject diagnostics
```

Pair that with refined field types (an `Email` with a private constructor) and "did anyone validate this?" stops being
a question the rest of the codebase can ask.

## Rules Are Data, Not Just Code

FluentValidation rules are lambdas inside a class: they can run, but nothing else can read them. Axial constraints are
inspectable metadata, so the same declaration also produces the JSON Schema/OpenAPI contract
(`JsonSchema.generate`), UI metadata (`Inspect.model`), a compiled JSON codec (`Json.compile`), and redisplayable form
errors. With FluentValidation, each of those is a separate artifact to keep in sync by hand.

## Where FluentValidation Fits Better

- Large existing C# codebases already organized around DTOs, model binding, and validator classes.
- Validation of objects you genuinely do not construct (third-party types, EF entities mid-flight).
- Teams that want C#-first fluent syntax rather than F# declarations.

Axial's equivalent of "validate an existing object" exists — `Schema.check schema model` (the
`Axial.Schema` module, not [`Axial.Validation.Validation`]({{< relref "/schema/error-handling/validation/" >}}) from Error
Handling, which it's built on) re-checks a trusted model, and `Rules` add contextual requirements — but they run
against schema metadata, not a second rulebook.

## Side By Side

| Concern | FluentValidation | Axial |
| --- | --- | --- |
| Invalid object exists? | Yes, until validated | No — parsing constructs or fails |
| Rules readable as data | No (lambdas in classes) | Yes (`Constraint` metadata) |
| OpenAPI/JSON Schema | Separate annotations | Generated from the same declaration |
| Error paths | Property names via expressions | Structural paths (`contacts[1].value`) |
| Reflection | Expression trees + reflection | None; AOT/trimming/Fable-safe |
