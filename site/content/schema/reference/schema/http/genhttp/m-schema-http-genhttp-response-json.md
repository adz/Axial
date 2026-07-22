---
title: "Schema.Http.GenHttp.Response.json"
linkTitle: "json"
weight: 2400
type: docs
---

Serializes a trusted value as JSON through a compiled codec.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.Response.json&#32;<span>status&#32;codec&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `status` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.responsestatus">ResponseStatus</a></code> | The successful HTTP status. |
| `codec` | <code><span><a href="../../../codec/t-schema-json-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> | The compiled codec for the trusted output type. |
| `value` | <code>'model</code> | The trusted output value. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../../../../flow/reference/service/http/t-flow-httpclient-httpresponse.md">HttpResponse</a></code> | A request-relative GenHTTP response plan. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">json</span> <span class="id">ResponseStatus</span><span class="pn">.</span><span class="id">Created</span> <span class="id">Signup</span><span class="pn">.</span><span class="id">codec</span> <span class="id">signup</span>
</code></pre>
