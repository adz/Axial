---
title: "Flow.Http.DSL.fetch"
linkTitle: "fetch"
weight: 2619
type: docs
---

 Sends the request and returns the full transcript, failing on unexpected statuses.
 <example><code>GET $"{root}/users" |&gt; fetch</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.fetch&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<a href="t-flow-http-httpresponse.md">HttpResponse</a></span>&gt;</span></code> |  |
