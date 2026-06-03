---
title: "Ref"
linkTitle: "Ref<T>"
weight: 1000
type: docs
---


 Represents a handle to a mutable reference that can be updated atomically.


## Signature

<div class="fsdocs-usage">
<code>type Ref<'T></code>
</div>

## Type Parameters

| Name |
| --- |
| `T` |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">flow</span> <span class="pn">{</span>
     <span class="k">let!</span> <span class="id">r</span> <span class="o">=</span> <span class="id">Ref</span><span class="pn">.</span><span class="id">make</span> <span class="n">0</span>
     <span class="k">do!</span> <span class="id">Ref</span><span class="pn">.</span><span class="id">set</span> <span class="n">1</span> <span class="id">r</span>
     <span class="k">let!</span> <span class="id">v</span> <span class="o">=</span> <span class="id">Ref</span><span class="pn">.</span><span class="id">get</span> <span class="id">r</span>
     <span class="k">return</span> <span class="id">v</span>
 <span class="pn">}</span>
</code></pre>
