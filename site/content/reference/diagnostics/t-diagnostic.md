---
title: "Diagnostic"
linkTitle: "Diagnostic<error>"
weight: 1002
type: docs
---

A single failure item attached to a path in a validation graph.

## Signature

<div class="fsdocs-usage">
<code>type Diagnostic<'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |

## Record Fields

| Field | Description |
| --- | --- |
| `Path` | The path to the source of the error. |
| `Error` | The application-specific error value of type <code>&#39;error</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="pn">{</span> <span class="id">Path</span> <span class="o">=</span> <span class="pn">[</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;Email&quot;</span> <span class="pn">]</span><span class="pn">;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Error</span> <span class="o">=</span> <span class="s">&quot;Invalid format&quot;</span> <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: &#39;a</div>
<div popover class="fsdocs-tip" id="fs2">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
