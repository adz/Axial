---
title: "Schema.Http.AspNetCore.Response.native"
linkTitle: "native"
weight: 2403
---

Returns a host-native ASP.NET result unchanged.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Response.native&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> | The result constructed through ASP.NET APIs. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></code> | The supplied result. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">return</span> <span class="id">Response</span><span class="pn">.</span><span class="id">native</span> <span class="pn">(</span><span class="id">Results</span><span class="pn">.</span><span class="id">Redirect</span> <span class="s">&quot;/login&quot;</span><span class="pn">)</span>
</code></pre>
