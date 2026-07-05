---
title: "Schema.ValueShape"
linkTitle: "ValueShape"
weight: 1200
---


 Describes the shape of a value schema as inspectable metadata for non-validation interpreters.


## Signature

<div class="fsdocs-usage">
<code>type ValueShape</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Primitive` | A primitive value of the supplied kind. |
| `Refined` | A refined/domain value whose boundary representation is the supplied underlying description. |
| `Nested` | A nested model value described by its own field descriptions. |
| `Many` | A collection value whose items share the supplied item description. |
| `Union` | A tagged union value with explicit discriminator, payload field, and case descriptions. |

## Remarks

<p class='fsdocs-para'>
 Shape descriptions carry no getters, constructors, or executable checks. JSON Schema emitters, documentation
 generators, and UI metadata producers can walk them without parsing raw input or running validation.
 </p>
