---
title: "Flow.HttpClient.DSL.withRetries"
linkTitle: "withRetries"
weight: 2623
---

 Retries transient failures with exponential backoff.
 <example><code>GET $"{root}/users" |&gt; fetchJson decode |&gt; withRetries 4</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.withRetries&#32;<span>maxAttempts&#32;workflow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maxAttempts` | <code>int</code> |  |
| `workflow` | <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'b</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;'b</span>&gt;</span></code> |  |
