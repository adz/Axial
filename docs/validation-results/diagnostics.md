---
weight: 40
title: Diagnostics Graph
description: Structured validation diagnostics.
---

# Diagnostics Graph

Axial returns a structured **Diagnostics Graph** for validation failures. The graph records where each failure occurred.

## The Structure

The graph is a tree where each node can contain local errors and child branches.

```fsharp
type PathSegment =
    | Key of string    // A record field or property
    | Index of int     // A position in a list or sequence
    | Name of string   // A custom label for a validation branch

type Diagnostic<'error> =
    { Path: PathSegment list
      Error: 'error }

type Diagnostics<'error> =
    { Errors: 'error list
      Children: Map<PathSegment, Diagnostics<'error>> }
```

## Creating Scoped Diagnostics

Inside a `validate {}` block, you use helpers to push diagnostics into the tree:

- `validate.key "address"` -> Failures appear under `.address`
- `validate.index 0` -> Failures appear under `.[0]`
- `validate.name "Shipping"` -> Failures appear under `.Shipping`

```fsharp
let validateAddress addr = 
    validate.key "address" {
        let! city = validate.name "City" { ... }
        return ...
    }
```

## Consuming Diagnostics

Once you have a `Validation<'v, 'e>` result, you can transform the graph for the user:

### 1. Flattening for APIs
Use `Diagnostics.flatten` to turn the tree into a flat list of path-bearing errors. This is the standard pattern for JSON APIs.

```fsharp
// Path: [Key "customer"; Key "address"; Name "City"]
// Error: "Required"
// Flat: "customer.address.City: Required"
```

### 2. Human-Readable Output
Use `Diagnostics.toString` to render the graph as a compact, YAML-like tree for logs or console output.

```yaml
customer:
  address:
    City:
      - Required
```

## Mapping and Filtering

Diagnostics are generic over the `'error` type. You can use `Validation.mapError` (or `Diagnostics.map` directly) to translate internal domain errors into user-facing localized strings at the application boundary.

## Example

For a runnable example with nested validation, path rendering, and JSON API error formatting, see the [Diagnostics Example](../patterns/examples/#diagnostics-example) in the examples gallery.

You can also view the [source code directly on GitHub](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/DiagnosticsExample.fs).
