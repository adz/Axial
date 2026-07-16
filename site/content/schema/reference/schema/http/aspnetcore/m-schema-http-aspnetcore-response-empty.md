---
title: "Schema.Http.AspNetCore.Response.empty"
linkTitle: "empty"
weight: 2402
type: docs
---

Returns an empty response with the supplied status code.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Response.empty&#32;<span>statusCode</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `statusCode` | <code>int</code> | The successful HTTP status code. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> | An empty ASP.NET result. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">empty</span> <span class="n">204</span>
</code></pre>
