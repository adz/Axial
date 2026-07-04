---
title: "Validation.Validation.at"
linkTitle: "at"
weight: 2200
---

Scopes a validation under the supplied path segments.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.at&#32;<span>path&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code><span><a href="../diagnostics/t-validation-pathsegment.md">PathSegment</a>&#32;list</span></code> | The path segments to apply to the validation. |
| `validation` | <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to scope. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A validation nested under the given path. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span><span class="pn">)</span>
 <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">at</span> <span class="pn">[</span><span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;user&quot;</span><span class="pn">]</span>
</code></pre>
