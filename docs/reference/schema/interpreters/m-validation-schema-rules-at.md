---
title: "Validation.Schema.Rules.at"
linkTitle: "at"
weight: 2403
---

Scopes a rule&#39;s diagnostics under the supplied path when the rule fails.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.Rules.at&#32;<span>path&#32;rule</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code><a href="/reference/Axial/axial-validation-path.html">Path</a></code> | The path segments to prefix to the rule&#39;s diagnostics. |
| `rule` | <code><span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span></code> | The rule whose failures should be scoped. |

## Returns

| Type | Description |
| --- | --- |
| <code><span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">scoped</span> <span class="o">=</span> <span class="id">needsReview</span> <span class="o">|&gt;</span> <span class="id">Rules</span><span class="pn">.</span><span class="id">at</span> <span class="pn">[</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;approval&quot;</span><span class="pn">;</span> <span class="id">PathSegment</span><span class="pn">.</span><span class="id">Name</span> <span class="s">&quot;reviewer&quot;</span> <span class="pn">]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val scoped: obj</div>
