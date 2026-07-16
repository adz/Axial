---
title: "Schema.Http.AspNetCore.Request.raw"
linkTitle: "raw"
weight: 2200
---

Projects untrusted input directly from the native request without schema parsing.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Request.raw&#32;<span>projection</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `projection` | <code><span><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httprequest">HttpRequest</a>&#32;->&#32;'input</span></code> | The direct projection from the native request. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span><span><a href="t-schema-http-aspnetcore-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-aspnetcore-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;'input</span>&gt;</span></code> | An endpoint Flow containing the projected, still-untrusted value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">signature</span> <span class="o">=</span> <span class="id">Request</span><span class="pn">.</span><span class="id">raw</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">request</span> <span class="k">-&gt;</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span> <span class="id">request</span><span class="pn">.</span><span class="id">Headers</span><span class="pn">[</span><span class="s">&quot;x-signature&quot;</span><span class="pn">]</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val signature: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
