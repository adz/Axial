---
title: "Flow.HttpClient.HttpError.isTransient"
linkTitle: "isTransient"
weight: 2202
type: docs
---

 Indicates whether retrying the same request could plausibly succeed.
 Connection failures, timeouts, and 408/429/5xx statuses are transient.
 <example><code>if HttpError.isTransient error then retry ()</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.HttpError.isTransient&#32;<span>_arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `_arg1` | <code><a href="t-flow-httpclient-httperror.md">HttpError</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>bool</code> |  |
