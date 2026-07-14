---
title: "Flow.HttpClient.Http.bytes"
linkTitle: "bytes"
weight: 2509
type: docs
---

 Sends a request and returns the exact response body bytes.
 <example><code>Http.get url |&gt; Http.bytes</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.bytes&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;<span>byte&#32;array</span></span>&gt;</span></code> |  |
