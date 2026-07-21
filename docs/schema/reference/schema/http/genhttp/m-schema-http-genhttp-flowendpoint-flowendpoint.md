---
title: "Schema.Http.GenHttp.FlowEndpoint.flowEndpoint"
linkTitle: "flowEndpoint"
weight: 2500
---

Lowers an endpoint Flow to the native handler expected by GenHTTP routing.

## Signature

<div class="fsdocs-usage">
<code><span>flowEndpoint&#32;<span>getAppEnvironment&#32;mapApplicationError&#32;workflow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `getAppEnvironment` | <code><span><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a>&#32;->&#32;'app</span></code> | Constructs or resolves the explicit application environment for the current request. |
| `mapApplicationError` | <code><span>'error&#32;->&#32;<a href="t-schema-http-genhttp-httpresponse.md">HttpResponse</a></span></code> | Maps one expected application failure to a GenHTTP response plan. |
| `workflow` | <code><span><a href="https://learn.microsoft.com/dotnet/api/axial.flow.flow-3">Flow</a>&lt;<span><span><a href="t-schema-http-genhttp-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-genhttp-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;<a href="t-schema-http-genhttp-httpresponse.md">HttpResponse</a></span>&gt;</span></code> | The complete endpoint Flow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.func-2">Func</a>&lt;<span><a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.irequest">IRequest</a>,&#32;<span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/genhttp.api.protocol.iresponse">IResponse</a>&gt;</span></span>&gt;</span></code> | A native delegate suitable for GenHTTP routing methods. |

## Remarks


 Invalid requests become RFC 9457 responses and typed application failures use <code>mapApplicationError</code>.
 A single defect is rethrown unchanged, multiple defects become <code>AggregateException</code>, interruption becomes
 <code>OperationCanceledException</code>, and compound typed-only causes are rejected rather than reduced to an arbitrary
 failure. GenHTTP does not supply a request cancellation token through this adapter.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">endpoint</span> <span class="o">=</span> <span class="id">flowEndpoint</span> <span class="id">getAppEnvironment</span> <span class="id">ApiError</span><span class="pn">.</span><span class="id">toResponse</span>
 <span class="id">Inline</span><span class="pn">.</span><span class="id">Create</span><span class="pn">(</span><span class="pn">)</span><span class="pn">.</span><span class="id">Post</span><span class="pn">(</span><span class="s">&quot;/signups&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">endpoint</span> <span class="id">signupEndpoint</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val endpoint: obj</div>
