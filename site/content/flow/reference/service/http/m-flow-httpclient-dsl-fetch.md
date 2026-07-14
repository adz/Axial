---
title: "Flow.HttpClient.DSL.fetch"
linkTitle: "fetch"
weight: 2619
type: docs
---

 Sends the request and returns the full transcript, failing on unexpected statuses.
 <example><code>GET $"{root}/users" |&gt; fetch</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.fetch&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;<a href="t-flow-httpclient-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
