---
title: "Flow.HttpClient.DSL.jsonBodyOf"
linkTitle: "jsonBodyOf"
weight: 2614
type: docs
---

 Encodes and sends a JSON body. <example><code>request |&gt; jsonBodyOf (Json.serialize codec) user</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.jsonBodyOf&#32;<span>encode&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `encode` | <code><span>'a&#32;->&#32;string</span></code> |  |
| `value` | <code>'a</code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
