---
title: "Flow.HttpClient.Http.text"
linkTitle: "text"
weight: 2508
---

 Sends a request and returns the response body text.
 <example><code>Http.get url |&gt; Request.bearer token |&gt; Http.text</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Http.text&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-httpclient-httperror.md">HttpError</a>,&#32;string</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow.HttpClient/Http.fs#L399-399)
