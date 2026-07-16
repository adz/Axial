---
title: "Schema.Http.AspNetCore.FlowEndpoint.flowEndpoint"
linkTitle: "flowEndpoint"
weight: 2500
type: docs
---

Lowers an endpoint Flow to the native ASP.NET Core handler expected by minimal-API routing.

## Signature

<div class="fsdocs-usage">
<code><span>flowEndpoint&#32;<span>getAppEnvironment&#32;mapApplicationError&#32;workflow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `getAppEnvironment` | <code><span><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext">HttpContext</a>&#32;->&#32;'app</span></code> | Constructs or resolves the explicit application environment for the current request. |
| `mapApplicationError` | <code><span>'error&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></span></code> | Maps one expected application failure to an ASP.NET result. |
| `workflow` | <code><span><a href="../../../flow/t-flow-flow.md">Flow</a>&lt;<span><span><a href="t-schema-http-aspnetcore-httpendpointenv.md">HttpEndpointEnv</a>&lt;'app&gt;</span>,&#32;<span><a href="t-schema-http-aspnetcore-endpointerror.md">EndpointError</a>&lt;'error&gt;</span>,&#32;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a></span>&gt;</span></code> | The complete endpoint Flow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.func-2">Func</a>&lt;<span><a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext">HttpContext</a>,&#32;<span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<a href="https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iresult">IResult</a>&gt;</span></span>&gt;</span></code> | A native delegate suitable for <code>MapGet</code>, <code>MapPost</code>, and the other ASP.NET routing methods. |

## Remarks


 Invalid requests become RFC 9457 responses and typed application failures use <code>mapApplicationError</code>.
 A single defect is rethrown unchanged, multiple defects become <code>AggregateException</code>, interruption becomes
 <code>OperationCanceledException</code> with <code>HttpContext.RequestAborted</code>, and compound typed-only causes are rejected
 rather than reduced to an arbitrary failure.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">endpoint</span> <span class="o">=</span> <span class="id">flowEndpoint</span> <span class="id">AppEnv</span><span class="pn">.</span><span class="id">fromContext</span> <span class="id">ApiError</span><span class="pn">.</span><span class="id">toResponse</span>
 <span class="id">app</span><span class="pn">.</span><span class="id">MapPost</span><span class="pn">(</span><span class="s">&quot;/signups&quot;</span><span class="pn">,</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">endpoint</span> <span class="id">signupEndpoint</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val endpoint: obj</div>
