---
title: Derived Schemas
linkTitle: Derived Schemas
description: Generate Schema declarations from ordinary F# records and attributes.
type: docs
weight: 60
---

# Derived Schemas

`[<DeriveSchema>]` removes the mechanical duplication between a wire record and its `Schema<_>` declaration. The
build reads ordinary F# source, infers a schema from each marked record, and generates normal constructor-last Schema
DSL code before F# compilation.

```fsharp
namespace MyApp.Wire

open Axial.Schema.Derive

[<DeriveSchema>]
type Signup =
    { [<Email; Present>]
      Email: string
      [<AtLeast 18>]
      Age: int
      Tags: string list }
```

The generated companion module provides:

```fsharp
Signup.schema    // Schema<Signup>
Signup.parse     // Data -> Result<Signup, SchemaErrors>
Signup.validate  // Signup -> Result<Signup, SchemaErrors>
```

This is source generation, not runtime reflection. The attributes are inert metadata; the generated F# uses the same
[Schema DSL](../syntax/) as a hand-written declaration and therefore works with parsing, inspection, JSON codecs,
JSON Schema, NativeAOT, trimming, and Fable.

## Guides

- [Set up build generation](./msbuild/) — package reference, generated-file modes, and MSBuild properties.
- [Attributes](./attributes/) — every supported attribute and its Schema DSL equivalent.
- [How inference works](./inference/) — records, field types, names, constructors, unions, and diagnostics.

## When to derive

Derivation is intended for public, permissive boundary records. Keep domain invariants in refined values or private
domain types, then map the parsed wire record through a domain constructor. See
[Separate Wire and Domain Models](../patterns/wire-and-domain-models/).
