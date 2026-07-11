---
title: "Flow.Http.send"
linkTitle: "send"
weight: 2506
type: docs
---

 Sends a request and fails with <c>HttpError.Status</c> when the response is outside the expectation.
 <example><code>request |&gt; Http.send</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.send&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<a href="t-flow-http-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
