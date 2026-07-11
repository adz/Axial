---
title: "Schema.ContextRules.apply"
linkTitle: "apply"
weight: 2500
type: docs
---

Applies contextual rules to an already-trusted model, accumulating any diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.ContextRules.apply&#32;<span>rules&#32;model</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `rules` | <code><span><span>(<span>'model&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></span>)</span>&#32;list</span></code> | The rules to evaluate, in order. |
| `model` | <code>'model</code> | The already-trusted model to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'model,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></code> |  |

## Remarks


 The model is not constructed, parsed, or transformed. Every rule is evaluated against the same trusted
 instance; all failures merge into one diagnostics graph. An empty rule list accepts every model.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">rulesFor</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">stage</span> <span class="o">=</span>
     <span class="k">match</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="3" class="fn">stage</span> <span class="k">with</span>
     <span class="pn">|</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="fn">ManagerReview</span> <span class="k">-&gt;</span> <span class="pn">[</span> <span class="id">needsAssignee</span><span class="pn">;</span> <span class="id">mustHaveDueDate</span> <span class="pn">]</span>
     <span class="pn">|</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="fn">FinalAudit</span> <span class="k">-&gt;</span> <span class="pn">[</span> <span class="id">mustHaveApprovalTrail</span> <span class="pn">]</span>
     <span class="pn">|</span> <span data-fsdocs-tip="fs5" data-fsdocs-tip-unique="6" class="fn">Draft</span> <span class="k">-&gt;</span> <span class="pn">[</span><span class="pn">]</span>

 <span class="k">match</span> <span class="id">ContextRules</span><span class="pn">.</span><span class="id">apply</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="7" class="id">rulesFor</span> <span class="id">ticket</span><span class="pn">.</span><span class="id">Stage</span><span class="pn">)</span> <span class="id">ticket</span> <span class="k">with</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs6" data-fsdocs-tip-unique="8" class="uc">Ok</span> <span data-fsdocs-tip="fs7" data-fsdocs-tip-unique="9" class="fn">trusted</span> <span class="k">-&gt;</span> <span class="id">approve</span> <span data-fsdocs-tip="fs7" data-fsdocs-tip-unique="10" class="id">trusted</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs8" data-fsdocs-tip-unique="11" class="uc">Error</span> <span data-fsdocs-tip="fs9" data-fsdocs-tip-unique="12" class="fn">diagnostics</span> <span class="k">-&gt;</span> <span class="id">reject</span> <span data-fsdocs-tip="fs9" data-fsdocs-tip-unique="13" class="id">diagnostics</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val rulesFor: stage: &#39;a -&gt; &#39;b list</div>
<div popover class="fsdocs-tip" id="fs2">val stage: &#39;a</div>
<div popover class="fsdocs-tip" id="fs3">val ManagerReview: &#39;a</div>
<div popover class="fsdocs-tip" id="fs4">val FinalAudit: &#39;a</div>
<div popover class="fsdocs-tip" id="fs5">val Draft: &#39;a</div>
<div popover class="fsdocs-tip" id="fs6">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs7">val trusted: obj</div>
<div popover class="fsdocs-tip" id="fs8">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs9">val diagnostics: obj</div>
