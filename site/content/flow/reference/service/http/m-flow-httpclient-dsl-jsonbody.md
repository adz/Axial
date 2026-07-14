---
title: "Flow.HttpClient.DSL.jsonBody"
linkTitle: "jsonBody"
weight: 2613
type: docs
---

 Sends an already-serialized JSON body. <example><code>request |&gt; jsonBody """{"name":"Ada"}"""</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.jsonBody&#32;<span>json&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `json` | <code>string</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
