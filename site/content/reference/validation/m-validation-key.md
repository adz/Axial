---
title: "Validation.key"
linkTitle: "key"
weight: 2201
type: docs
---

Prefixes a validation with a keyed branch.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.key&#32;<span>Validation.key&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `key` | <code>string</code> | The branch key. |
| `validation` | <code><span><a href="t-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to scope. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A validation whose diagnostics are prefixed with <code>Key key</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span><span class="pn">)</span>
 <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">key</span> <span class="s">&quot;id-123&quot;</span>
</code></pre>
