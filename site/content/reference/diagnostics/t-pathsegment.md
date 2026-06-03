---
title: "PathSegment"
linkTitle: "PathSegment"
weight: 1000
type: docs
---

Location markers used to describe where a diagnostic belongs in a validation graph.

## Signature

<div class="fsdocs-usage">
<code>type PathSegment</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Key` | A string-based key, usually for map or record fields. |
| `Index` | A zero-based integer index, usually for lists or arrays. |
| `Name` | A descriptive name for a property or field. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">s1</span> <span class="o">=</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Key</span> <span class="s">&quot;user-123&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">s2</span> <span class="o">=</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Index</span> <span class="n">0</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">s3</span> <span class="o">=</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;Email&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val s1: obj</div>
<div popover class="fsdocs-tip" id="fs2">val s2: obj</div>
<div popover class="fsdocs-tip" id="fs3">val s3: obj</div>
