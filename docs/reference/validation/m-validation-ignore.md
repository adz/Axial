---
title: "Validation.ignore"
linkTitle: "ignore"
weight: 2112
---

Maps a successful validation value to <code>unit</code> while preserving the diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.ignore&#32;<span>validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validation` | <code><span><a href="t-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation.md">Validation</a>&lt;<span>unit,&#32;'error</span>&gt;</span></code> | A validation that keeps the original diagnostics and discards the success value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ignore</span> <span class="c">// Validation (Ok ())</span>
</code></pre>
