---
title: "Schema.Http.AspNetCore.Request.route"
linkTitle: "route"
weight: 2103
type: docs
---

Schema-parses one named ASP.NET route value.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.Request.route&#32;<span>name&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> | The route-value name registered in the ASP.NET route pattern. |
| `schema` | <code><span><a href="../../t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> | The schema that parses the scalar route text. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/axial.flow.flow-3">Flow</a>&lt;<span><span><a href="t-schema-http-aspnetcore-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-aspnetcore-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;'model</span>&gt;</span></code> | An endpoint Flow that succeeds with the trusted model. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">userId</span> <span class="o">=</span> <span class="id">Request</span><span class="pn">.</span><span class="id">route</span> <span class="s">&quot;id&quot;</span> <span class="id">UserId</span><span class="pn">.</span><span class="id">schema</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val userId: obj</div>
