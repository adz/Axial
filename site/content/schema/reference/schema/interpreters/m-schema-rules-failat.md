---
title: "Schema.Rules.failAt"
linkTitle: "failAt"
weight: 2504
type: docs
---

Creates a rule failure attached to the supplied diagnostics path.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Rules.failAt&#32;<span>path&#32;error</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code><a href="/reference/Axial/axial-validation-path.html">Path</a></code> | The diagnostics path that should receive the failure. |
| `error` | <code>'error</code> | The rule error to attach. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">result</span> <span class="o">=</span> <span class="id">Rules</span><span class="pn">.</span><span class="id">failAt</span> <span class="pn">[</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;assignee&quot;</span> <span class="pn">]</span> <span class="id">HighPriorityNeedsAssignee</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val result: obj</div>
