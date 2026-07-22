---
title: "Schema.Http.GenHttp.Response.text"
linkTitle: "text"
weight: 2401
---

Returns a plain-text response.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.Response.text&#32;<span>status&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `status` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.responsestatus">ResponseStatus</a></code> | The successful HTTP status. |
| `value` | <code>string</code> | The response text. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../../../../flow/reference/service/http/t-flow-httpclient-httpresponse.md">HttpResponse</a></code> | A request-relative plain-text response plan. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">text</span> <span class="id">ResponseStatus</span><span class="pn">.</span><span class="id">Ok</span> <span class="s">&quot;ready&quot;</span>
</code></pre>
