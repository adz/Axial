---
title: "Schema.SchemaError"
linkTitle: "SchemaError"
weight: 1200
---

Schema input, checking, and contextual rule failures attached to diagnostics paths.

## Signature

<div class="fsdocs-usage">
<code>type SchemaError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Omitted` | Required boundary input was not supplied. |
| `Blank` | Boundary input was present but carried no value. The parse-side lowering of a missing value. |
| `ExpectedScalar` | A scalar was expected at this path. |
| `ExpectedObject` | An object was expected at this path. |
| `ExpectedMany` | A collection was expected at this path. |
| `InvalidFormat` | The input could not be read as the named target type. |
| `ParseOutOfRange` | The input was well-formed but outside the target type's representable range. |
| `UnknownTag` | A union or enum discriminator did not name one of the declared cases. |
| `Violation` | The value was read successfully and then failed its constraint. |
| `ConstructorFailed` | The model constructor rejected an otherwise admissible set of field values. |
| `Custom` | A Schema-owned intrinsic check failed. |

## Remarks


 The parse/check axis is the organising split. Parsing cases mean the input could not be read as the declared
 type at all; <code>Violation</code> means it was read and then failed its constraint. Constraint failures are never
 lowered into a parse-shaped case, because a lowering that discards the atom forces consumers back to
 reconstructing constraint identity from strings.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaError.fs#L19-19)
