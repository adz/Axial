---
title: "Constraint.custom"
linkTitle: "custom"
weight: 2304
type: docs
---

Runs an arbitrary predicate, reporting the supplied prose when it fails.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.custom&#32;<span>description&#32;predicate</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `description` | <code>string</code> |  |
| `predicate` | <code><span>'value&#32;->&#32;bool</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks


 Opaque by construction. It executes and composes normally, may appear in Schema and refinements, and is
 documented honestly by exporters — but it is invisible to export enforcement and to proof, because an
 arbitrary host-language closure has no logical meaning to translate. No authored code or argument may claim
 inspectable logic.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">custom</span> <span class="s">&quot;must be a valid ISBN&quot;</span> <span class="id">isValidIsbn</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L224-224)
