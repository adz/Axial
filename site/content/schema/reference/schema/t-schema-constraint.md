---
title: "Schema.Constraint"
linkTitle: "Constraint"
weight: 1113
type: docs
---


 Describes a portable schema constraint as inspectable metadata.


## Signature

<div class="fsdocs-usage">
<code>type Constraint</code>
</div>

## Remarks

<p class='fsdocs-para'>
 Schema constraints are declarative data for interpreters. They are intentionally separate from executable check
 functions so input parsers, diagnostics, JSON Schema emitters, UI renderers, and documentation generators can inspect
 the same constraint without running validation logic.
 </p><p class='fsdocs-para'>
 The generic metadata shape comes before the named constraint helpers. Later helpers such as required, max length, and
 numeric ranges can create these values with stable codes and arguments while still lowering to executable checks in
 validation-oriented interpreters.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Constraints.fs#L85-85)
