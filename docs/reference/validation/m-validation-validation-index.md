---
title: "Validation.Validation.index"
linkTitle: "index"
weight: 2202
---

Prefixes a validation with an indexed branch.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.index&#32;<span>Validation.Validation.index&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `index` | <code>int</code> | The branch index. |
| `validation` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to scope. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A validation whose diagnostics are prefixed with <code>Index index</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span><span class="pn">)</span>
 <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">index</span> <span class="n">0</span>
</code></pre>
