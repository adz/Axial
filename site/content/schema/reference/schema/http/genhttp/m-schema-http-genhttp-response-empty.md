---
title: "Schema.Http.GenHttp.Response.empty"
linkTitle: "empty"
weight: 2402
type: docs
---

Returns an empty response with the supplied status.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.Response.empty&#32;<span>status</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `status` | <code><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.responsestatus">ResponseStatus</a></code> | The successful HTTP status. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../../../../flow/reference/service/http/t-flow-httpclient-httpresponse.md">HttpResponse</a></code> | A request-relative empty response plan. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">empty</span> <span class="id">ResponseStatus</span><span class="pn">.</span><span class="id">NoContent</span>
</code></pre>
