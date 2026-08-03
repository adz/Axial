---
title: "Constraint.Renderer"
linkTitle: "Renderer"
weight: 1900
---


 Renders localized messages for one document context and attribute. Immutable: build one at the composition
 root and derive scoped copies with <code>context</code> and <code>attribute</code>.


## Signature

<div class="fsdocs-usage">
<code>type Renderer</code>
</div>

## Remarks

<p class='fsdocs-para'>
 A renderer holds no violation and a violation holds no renderer. Context arrives here, at the rendering edge,
 which is what keeps <code>Violation</code> path-free, closure-free comparable data.
 </p><p class='fsdocs-para'><code>context</code> appends a document, model, form, or component segment. <code>attribute</code> replaces the whole
 attribute with one segment, so a form-scoped renderer is safe to reuse for sibling fields without leaking the
 previous field&#39;s noun.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L319-319)
