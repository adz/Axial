---
title: "Schema.Codec.JsonCodecException"
linkTitle: "JsonCodecException"
weight: 1001
---

The exception raised when JSON text cannot be decoded through a compiled schema codec.

## Signature

<div class="fsdocs-usage">
<code>type JsonCodecException</code>
</div>

## Remarks


 The path renders like <code>$.contacts[1].value</code>, matching the field names declared on the schema. Codec decoding
 is the trusted hot path: it reports the first structural failure and does not accumulate path-aware diagnostics.
 Use schema input parsing (<code>Schema.parse</code> over <code>Data</code>) at untrusted boundaries where complete
 diagnostics matter more than throughput.
