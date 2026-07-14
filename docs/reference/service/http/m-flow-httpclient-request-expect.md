---
title: "Flow.HttpClient.Request.expect"
linkTitle: "expect"
weight: 2316
---

 Replaces the statuses treated as success.
 <example><code>request |&gt; Request.expect [ 200; 404 ]</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.expect&#32;<span>statuses&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `statuses` | <code><span>int&#32;seq</span></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
