---
weight: 15
title: What Data represents
type: docs
description: The six Data cases and the structural rules shared by literals, JSON, edits, matching, and comparison.
---


`Data` represents structured values with six cases. The model maps directly to JSON and also works for fixtures,
configuration, command-line input, form values, events, and other tree-shaped data.

```fsharp
type Data =
    | Null
    | Text of string
    | Number of token: string
    | Bool of bool
    | List of Data list
    | Object of (string * Data) list
```

## `Data.Null`

`Null` is a present value with no scalar content. In JSON it renders as `null`.

```fsharp
Data.render Data.Null
// => "null"
```

A null field is different from an absent field. In literal syntax, `"value" => nil` includes a null field, while
`"value" ?=> None` omits the field.

## `Data.Text`

`Text` stores a .NET string and renders it as a JSON string with the required escaping.

```fsharp
Data.render (Data.Text "Ada\nLovelace")
// => "\"Ada\\nLovelace\""
```

A null .NET string converts to `Data.Null`; it does not become `Data.Text null`.

## `Data.Number`

`Number` stores the number as a token rather than converting every number to one CLR numeric type. This retains
large integers, decimal precision, trailing zeros, and exponent notation.

```fsharp
Data.Number "1.2300e+4"
```

The token is visible to exact comparison: `1`, `1.0`, and `1e0` are different. Use the `num` function rather than the
union case when accepting text, because `num` checks that the token is a valid JSON number.

See [Numbers](../numbers/) for conversion rules and examples.

## `Data.Bool`

`Bool` stores `true` or `false` and renders it without quotes.

```fsharp
Data.render (Data.Bool true)
// => "true"
```

## `Data.List`

`List` stores ordered `Data` values. Order and repeated values are preserved.

```fsharp
Data.render (Data.List [ Data.Text "author"; Data.Text "admin" ])
// => "[\"author\", \"admin\"]"
```

Exact comparison checks every item and its position. Matching can instead check an ordered subsequence, an unordered
subset, every item, or at least one item.

## `Data.Object`

`Object` stores an ordered list of name/value pairs. It preserves declaration order and duplicate field names.

```fsharp
let value =
    Data.Object [
        "name", Data.Text "Ada"
        "name", Data.Text "Grace"
    ]

Data.render value
// => "{ name: \"Ada\", name: \"Grace\" }"
```

Path lookup and edits select the last field when a name is repeated. Exact comparison observes every field, including
duplicates and order. Partial object matching consumes matching duplicate fields one occurrence at a time.

## Paths

`DataPath` identifies nested fields and zero-based list indexes. String paths use dots for ordinary field names,
brackets for indexes, and quoted brackets for names containing punctuation.

```fsharp
DataPath.parse "customer.roles[1]"
// => [ DataPathSegment.Name "customer"; DataPathSegment.Name "roles"; DataPathSegment.Index 1 ]

DataPath.parse "metadata[\"build.version\"]"
// => [ DataPathSegment.Name "metadata"; DataPathSegment.Name "build.version" ]
```

The empty path `""` addresses the root value.
