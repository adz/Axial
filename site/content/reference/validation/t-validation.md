---
title: "Validation"
linkTitle: "Validation<value, error>"
weight: 1000
type: docs
---


 An accumulating validation result that keeps the structured diagnostics graph visible.


## Signature

<div class="fsdocs-usage">
<code>type Validation<'value, 'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |
| `error` |

## Remarks


 Unlike <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">FSharpResult</a>, this type is designed for applicative
 composition using <code>and!</code> in the <code>validate { }</code> builder, which merges errors instead of
 short-circuiting.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v1</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">v2</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Error 1&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v1: obj</div>
<div popover class="fsdocs-tip" id="fs2">val v2: obj</div>
