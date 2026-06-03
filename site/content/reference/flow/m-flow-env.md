---
title: "Flow.env"
linkTitle: "env"
weight: 2309
type: docs
---

Reads the current environment as the successful flow value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.env&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'env</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> whose successful value is the current environment. |

## Remarks


 Use this when the next step genuinely needs the whole environment value, for example when
 passing a request context to another helper. For a single dependency or configuration value,
 prefer <code>Flow.read</code>; it keeps the dependency local and makes the workflow easier to scan.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">myFlow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">env</span> <span class="o">|&gt;</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">map</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">env</span> <span class="k">-&gt;</span> <span class="id">env</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val myFlow: obj</div>
