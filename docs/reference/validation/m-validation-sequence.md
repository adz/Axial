---
title: "Validation.sequence"
linkTitle: "sequence"
weight: 2116
---

Transforms a sequence of validations into a validation of a list.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.sequence&#32;<span>validations</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validations` | <code><span><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&#32;seq</span></code> | The input sequence. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span><span>'value&#32;list</span>,&#32;'error</span>&gt;</span></code> | A validation containing the list of values. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">1</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">sequence</span> <span class="c">// Validation (Ok [1])</span>
</code></pre>
