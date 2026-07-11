---
title: "Flow.Http.sendResult"
linkTitle: "sendResult"
weight: 2507
---

 Sends a request and returns the transcript without interpreting the status expectation.
 <example><code>request |&gt; Http.sendResult</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.sendResult&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<a href="t-flow-http-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
