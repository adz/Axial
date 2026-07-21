---
title: "Schema.Http.AspNetCore.Request.native"
linkTitle: "native"
weight: 2201
---

Returns the native ASP.NET request for host-specific boundary handling.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Request.native&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/axial.flow.flow-3">Flow</a>&lt;<span><span><a href="t-schema-http-aspnetcore-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-aspnetcore-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httprequest">HttpRequest</a></span>&gt;</span></code> | An endpoint Flow containing the current native request. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">request</span> <span class="o">=</span> <span class="id">Request</span><span class="pn">.</span><span class="id">native</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val request: obj</div>
