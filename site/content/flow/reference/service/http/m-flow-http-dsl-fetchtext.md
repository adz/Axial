---
title: "Flow.Http.DSL.fetchText"
linkTitle: "fetchText"
weight: 2620
type: docs
---

 Sends the request and returns the body text. <example><code>GET $"{root}/readme" |&gt; fetchText</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.fetchText&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;string</span>&gt;</span></code> |  |
