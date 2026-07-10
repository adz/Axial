---
title: "Schema.Rules.apply"
linkTitle: "apply"
weight: 2507
---

Applies contextual rules to an already-trusted model, returning a plain result.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Rules.apply&#32;<span>ruleSet&#32;model</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `ruleSet` | <code><span><a href="t-schema-ruleset.md">RuleSet</a>&lt;<span>'model,&#32;'error</span>&gt;</span></code> | The rule set to evaluate. |
| `model` | <code>'model</code> | The already-trusted model to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'model,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 The supplied model is returned unchanged on success. Rules never construct, parse, or transform the model;
 they only decide whether the same trusted instance is acceptable in the current context.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">match</span> <span class="id">Rules</span><span class="pn">.</span><span class="id">apply</span> <span class="id">ticketRules</span> <span class="id">ticket</span> <span class="k">with</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Ok</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">trusted</span> <span class="k">-&gt;</span> <span class="id">handle</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="3" class="id">trusted</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="uc">Error</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="fn">diagnostics</span> <span class="k">-&gt;</span> <span class="id">reject</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="6" class="id">diagnostics</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs2">val trusted: obj</div>
<div popover class="fsdocs-tip" id="fs3">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs4">val diagnostics: obj</div>
