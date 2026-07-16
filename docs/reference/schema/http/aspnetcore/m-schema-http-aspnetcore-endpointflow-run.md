---
title: "Schema.Http.AspNetCore.EndpointFlow.run"
linkTitle: "run"
weight: 2300
---

Supplies <code>HttpEndpointEnv.App</code> to the application workflow and marks its typed failures as application errors.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Http.AspNetCore.EndpointFlow.run&#32;<span>operation&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span>'input&#32;->&#32;<span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span>'app,&#32;'error,&#32;'output</span>&gt;</span></span></code> | The HTTP-independent application workflow factory. |
| `input` | <code>'input</code> | The trusted input supplied to the application operation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span><span><a href="t-schema-http-aspnetcore-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-aspnetcore-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;'output</span>&gt;</span></code> | The application operation adapted to the endpoint environment and error channel. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let!</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="fn">created</span> <span class="o">=</span> <span class="id">EndpointFlow</span><span class="pn">.</span><span class="id">run</span> <span class="id">createSignup</span> <span class="id">signup</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val created: obj</div>
