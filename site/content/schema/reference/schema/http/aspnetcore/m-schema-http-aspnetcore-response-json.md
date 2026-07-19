---
title: "Schema.Http.AspNetCore.Response.json"
linkTitle: "json"
weight: 2400
type: docs
---

Streams a trusted value as JSON through a compiled codec.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Response.json&#32;<span>statusCode&#32;codec&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `statusCode` | <code>int</code> | The successful HTTP status code. |
| `codec` | <code><span><a href="../../../codec/t-schema-codec-jsoncodec.md">JsonCodec</a>&lt;'model&gt;</span></code> | The compiled codec for the trusted output type. |
| `value` | <code>'model</code> | The trusted output value. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> | An ASP.NET result that streams the encoded JSON. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">json</span> <span class="n">201</span> <span class="id">Signup</span><span class="pn">.</span><span class="id">codec</span> <span class="id">signup</span>
</code></pre>
