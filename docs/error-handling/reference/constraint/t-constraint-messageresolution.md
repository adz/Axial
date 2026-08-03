---
title: "Constraint.MessageResolution"
linkTitle: "MessageResolution"
weight: 1903
---

What an advanced resolver found for one contextual level.

## Signature

<div class="fsdocs-usage">
<code>type MessageResolution</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Template` | A template Axial should interpolate and format. |
| `Rendered` | Text the resolver has already rendered. Axial never interpolates it again. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L31-31)
