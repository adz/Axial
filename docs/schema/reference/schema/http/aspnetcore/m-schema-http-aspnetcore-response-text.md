---
title: "Schema.Http.AspNetCore.Response.text"
linkTitle: "text"
weight: 2401
---

Returns a plain-text response.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Response.text&#32;<span>statusCode&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `statusCode` | <code>int</code> | The successful HTTP status code. |
| `value` | <code>string</code> | The response text. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> | An ASP.NET plain-text result. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">text</span> <span class="n">200</span> <span class="s">&quot;ready&quot;</span>
</code></pre>
