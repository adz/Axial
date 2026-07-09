---
title: "Path"
linkTitle: "Path"
weight: 1001
---

A path through a validation graph, represented as a list of <a href="t-validation-pathsegment.md">PathSegment</a>.

## Signature

<div class="fsdocs-usage">
<code>type Path</code>
</div>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">path</span><span class="pn">:</span> <span class="id">Path</span> <span class="o">=</span> <span class="pn">[</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;User&quot;</span><span class="pn">;</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Index</span> <span class="n">0</span><span class="pn">;</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;Email&quot;</span> <span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val path: obj list</div>
