---
title: "Schema.SchemaShape"
linkTitle: "SchemaShape"
weight: 1300
---


 Describes the shape of a value schema as inspectable metadata for non-validation interpreters.


## Signature

<div class="fsdocs-usage">
<code>type SchemaShape</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Primitive` | A primitive value of the supplied kind. |
| `Refined` | A refined/domain value whose boundary representation is the supplied underlying description. |
| `Nested` | A nested model value described by its own field descriptions. |
| `Many` | A collection value whose items share the supplied item description. |
| `Union` | A tagged union value with explicit discriminator, payload field, and case descriptions. |
| `UnionInline` | An internally-tagged union value whose case payload fields sit beside the discriminator field. |
| `Enum` | A bare-string enum value with explicit case tags. |
| `Optional` | An optional value whose present payload is described by the supplied payload description. |
| `MapOf` | A dictionary value, keyed by text, whose entries share the supplied item description. |
| `Deferred` | The first expansion of a deferred recursive value, identified within this inspection tree. |
| `Recursive` | A reference back to an already-expanding deferred value. |

## Remarks

<p class='fsdocs-para'>
 Shape descriptions carry no getters, constructors, or executable checks. JSON Schema emitters, documentation
 generators, and UI metadata producers can walk them without parsing structured data or running validation.
 </p>
