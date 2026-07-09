---
title: "Codec.Json.compile"
linkTitle: "compile"
weight: 2100
---

Compiles a built model schema into a reusable JSON codec.

## Signature

<div class="fsdocs-usage">
<code><span>Codec.Json.compile&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="../schema/t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 Compile once per schema, typically at startup, and reuse the codec for every value. Schemas produced by
 <code>Schema.build</code> compile through the retained typed field chain into constructor-specialized plans. Schemas
 produced by <code>Schema.buildResult</code>/<code>Schema.buildResultWith</code> compile through the type-erased plan, and
 constructor errors surface as <a href="t-codec-jsoncodecexception.md">JsonCodecException</a> during decoding.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">codec</span> <span class="o">=</span> <span class="id">Json</span><span class="pn">.</span><span class="id">compile</span> <span class="id">customerSchema</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">json</span> <span class="o">=</span> <span class="id">Json</span><span class="pn">.</span><span class="id">serialize</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">codec</span> <span class="id">customer</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="4" class="id">roundTripped</span> <span class="o">=</span> <span class="id">Json</span><span class="pn">.</span><span class="id">deserialize</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="5" class="id">codec</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="6" class="id">json</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val codec: obj</div>
<div popover class="fsdocs-tip" id="fs2">val json: obj</div>
<div popover class="fsdocs-tip" id="fs3">val roundTripped: obj</div>
