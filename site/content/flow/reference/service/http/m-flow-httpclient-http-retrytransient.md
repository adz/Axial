---
title: "Flow.HttpClient.Http.retryTransient"
linkTitle: "retryTransient"
weight: 2516
type: docs
---

 Retries a workflow on transient HTTP errors with exponential backoff.
 Permanent failures such as 404 or decode errors are never retried.
 <example><code>Http.getJson decode url |&gt; Http.retryTransient 4 (TimeSpan.FromMilliseconds 200.0)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.retryTransient&#32;<span>maxAttempts&#32;baseDelay&#32;workflow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maxAttempts` | <code>int</code> |  |
| `baseDelay` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `workflow` | <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'value</span>&gt;</span></code> |  |
