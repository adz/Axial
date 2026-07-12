---
title: "Codec.JsonCodec"
linkTitle: "JsonCodec<model>"
weight: 1000
---

A compiled JSON codec for one schema-described model.

## Signature

<div class="fsdocs-usage">
<code>type JsonCodec<'model></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |

## Remarks

<p class='fsdocs-para'>
 Compile once with <a href="/reference/Axial/axial-codec-json.html">Json.compile</a> and reuse the codec for every value. Compilation
 walks the schema&#39;s retained typed field chain into a direct record plan — ordered field descriptors, cached wire-name
 bytes, and typed field decoders applied to the original curried constructor — so per-value encoding and decoding
 use no reflection and no boxed <code>obj array</code> dispatch for record fields.
 </p><p class='fsdocs-para'>
 The codec is the trusted hot path: it enforces JSON structure and required fields, but does not run schema
 constraint metadata such as <code>maxLength</code>. Parse untrusted boundary input with schema input parsing
 (<code>Schema.parse</code>) when complete path-aware diagnostics are needed, and use the codec where the payload producer
 is trusted, such as internal services, storage, caches, and message queues.
 </p>
