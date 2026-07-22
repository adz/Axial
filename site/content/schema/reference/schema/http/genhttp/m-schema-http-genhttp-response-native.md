---
title: "Schema.Http.GenHttp.Response.native"
linkTitle: "native"
weight: 2403
type: docs
---

Builds a host-native response plan from the current GenHTTP request.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.GenHttp.Response.native&#32;<span>respond</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `respond` | <code><span><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a></span></code> | The host-specific response function. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../../../../flow/reference/service/http/t-flow-httpclient-httpresponse.md">HttpResponse</a></code> | A request-relative response plan. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">native</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">request</span> <span class="k">-&gt;</span> <span class="id">request</span><span class="pn">.</span><span class="id">Respond</span><span class="pn">(</span><span class="pn">)</span><span class="pn">.</span><span class="id">Build</span><span class="pn">(</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
