---
title: "Flow.HttpClient.HttpError.transientPolicy"
linkTitle: "transientPolicy"
weight: 2203
---

 Builds a retry policy with exponential backoff that retries only transient HTTP errors.
 <example><code>workflow |&gt; Flow.Runtime.retry (HttpError.transientPolicy 4 (TimeSpan.FromMilliseconds 200.0))</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.HttpError.transientPolicy&#32;<span>maxAttempts&#32;baseDelay</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maxAttempts` | <code>int</code> |  |
| `baseDelay` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/runtime/t-flow-retrypolicy.md">RetryPolicy</a>&lt;<a href="t-flow-httpclient-httperror.md">HttpError</a>&gt;</span></code> |  |
