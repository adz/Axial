---
weight: 40
title: Diagnostics Graph
description: Structured validation diagnostics.
---

# Diagnostics

`Validation<'value, 'error>` uses `Diagnostics<'error>` as its failure value. Validation decides whether checks
succeed or accumulate; Diagnostics stores the accumulated errors and their locations.

A Diagnostics value is a tree. Each node can hold errors for its current location and child nodes for fields, list
indexes, or named validation branches.

## The structure

```fsharp
type PathSegment =
    | Key of string
    | Index of int
    | Name of string

type Diagnostic<'error> =
    { Path: PathSegment list
      Error: 'error }

type Diagnostics<'error> =
    { Errors: 'error list
      Children: Map<PathSegment, Diagnostics<'error>> }
```

Most code does not construct this record directly. It creates a Validation and adds a path with `Validation.key`,
`Validation.index`, or `Validation.name`. The `validate` builder provides the same operations.

## Attach locations while validating

Inside `validate {}`, path helpers put any failures from the nested block under a child node:

- `validate.key "address"` puts failures under the key `address`.
- `validate.index 0` puts failures under list index `0`.
- `validate.name "Shipping"` puts failures under a named branch.

```fsharp
let validateAddress address =
    validate.key "address" {
        let! city = validate.name "city" { return! requireCity address.City }
        return { address with City = city }
    }
```

If `requireCity` fails, the resulting path is `address.city`. Nesting another key or index extends that same path.

## Consume diagnostics at a boundary

Keep Diagnostics structured while composing validations. Convert it only when a boundary needs a particular output
shape.

### Flatten for an API

`Diagnostics.flatten` returns `Diagnostic<'error> list`. Each item contains the full path and one error.

```fsharp
// Path: [Key "customer"; Key "address"; Name "city"]
// Error: "Required"
```

### Render for logs

`Diagnostics.toString` renders the tree as compact text suitable for logs or console output.

```yaml
customer:
  address:
    city:
      - Required
```

## Change the error type

Diagnostics is generic over the error type. Use `Validation.mapError` while working with a Validation, or
`Diagnostics.map` once you have the Diagnostics value.

## Example

For nested validation, path rendering, and JSON API formatting, see the
[Diagnostics Example]({{< relref "/schema/examples.md" >}}#diagnostics-example).

You can also view the [source code directly on GitHub](https://github.com/adz/Axial/blob/main/examples/Axial.Examples/DiagnosticsExample.fs).
