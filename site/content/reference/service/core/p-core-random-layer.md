---
title: "layer"
linkTitle: "layer"
weight: 2402
type: docs
---


 The <code>layer { }</code> computation expression for provisioning explicit service environments.


## Signature

<div class="fsdocs-usage">
<code><span>layer&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layerbuilder.html">LayerBuilder</a></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-layerbuilder.html">LayerBuilder</a> instance. |

## Remarks


 Use plain <code>let!</code> when a later provisioning step depends on an earlier value.
 Use sibling <code>and!</code> bindings when services are independent and can be provisioned
 through <code>Layer.merge</code> / <code>Layer.zipPar</code>.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">appLayer</span> <span class="o">=</span>
     <span class="id">layer</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">config</span> <span class="o">=</span> <span class="id">configLayer</span>

         <span class="k">let!</span> <span class="id">runtime</span> <span class="o">=</span> <span class="id">BaseRuntime</span><span class="pn">.</span><span class="id">live</span>
         <span class="k">and!</span> <span class="id">orders</span> <span class="o">=</span> <span class="id">Orders</span><span class="pn">.</span><span class="id">layer</span> <span class="id">config</span>

         <span class="k">return</span> <span class="pn">{</span> <span class="id">Runtime</span> <span class="o">=</span> <span class="id">runtime</span><span class="pn">;</span> <span class="id">Orders</span> <span class="o">=</span> <span class="id">orders</span> <span class="pn">}</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val appLayer: obj</div>
