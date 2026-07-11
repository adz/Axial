---
title: "Schema.Model"
linkTitle: "Model<model>"
weight: 1400
type: docs
---

A schema-validated model value. Only <code>Model.parse</code>-adjacent functions in this module can
 produce one, so holding a <code>Model&lt;&#39;model&gt;</code> is proof the value passed every schema constraint and
 constructor invariant.

## Signature

<div class="fsdocs-usage">
<code>type Model<'model></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |

## Remarks

<p class='fsdocs-para'>
 The wrapper separates &quot;the record shape&quot; from &quot;the trust claim&quot;: the underlying record can stay public and
 freely constructible — a draft, visibly untrusted by its type — while functions that require validity demand
 <code>Model&lt;&#39;model&gt;</code> in their signatures. Construct drafts with ordinary record literals (named fields, any
 order) and promote them with <code>Model.validate</code>.
 </p>
