---
title: "Schema.Model.validate"
linkTitle: "validate { }"
weight: 2401
---


 Validates a draft value against its schema and promotes it to a trusted <code>Model&lt;&#39;model&gt;</code>.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.Model.validate&#32;<span>schema&#32;draft</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |
| `draft` | <code>'model</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="t-schema-model.md">Model</a>&lt;'model&gt;</span>,&#32;<span><a href="../../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;<a href="t-schema-schemaerror.md">SchemaError</a>&gt;</span></span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 This is the named-field trusted construction door: build the draft with an ordinary record literal
 (named fields, any order, compiler-checked completeness), then promote it. Every field constraint runs,
 the model&#39;s own constructor is re-invoked so cross-field invariants hold, and only the <code>Ok</code> value
 carries the <code>Model&lt;&#39;model&gt;</code> proof.
 </p><pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">match</span> <span class="id">Model</span><span class="pn">.</span><span class="id">validate</span> <span class="id">SignupRequest</span><span class="pn">.</span><span class="id">schema</span> <span class="pn">{</span> <span class="id">Email</span> <span class="o">=</span> <span class="s">&quot;ada@example.com&quot;</span><span class="pn">;</span> <span class="id">Age</span> <span class="o">=</span> <span class="n">42</span> <span class="pn">}</span> <span class="k">with</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="uc">Ok</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="fn">signup</span> <span class="k">-&gt;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="3" class="fn">signup</span><span class="pn">.</span><span class="id">Value</span><span class="pn">.</span><span class="id">Email</span>
 <span class="pn">|</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="uc">Error</span> <span data-fsdocs-tip="fs4" data-fsdocs-tip-unique="5" class="fn">diagnostics</span> <span class="k">-&gt;</span> <span class="o">..</span><span class="pn">.</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Result.Ok: ResultValue: &#39;T -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs2">val signup: obj</div>
<div popover class="fsdocs-tip" id="fs3">union case Result.Error: ErrorValue: &#39;TError -&gt; Result&lt;&#39;T,&#39;TError&gt;</div>
<div popover class="fsdocs-tip" id="fs4">val diagnostics: obj</div>
