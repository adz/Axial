---
title: "Flow.HttpClient.Http.sendResult"
linkTitle: "sendResult"
weight: 2507
type: docs
---

 Sends a request and returns the transcript without interpreting the status expectation.
 <example><code>request |&gt; Http.sendResult</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.sendResult&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;<a href="t-flow-httpclient-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
