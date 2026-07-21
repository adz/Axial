---
title: "Flow.HttpClient.DSL.expect"
linkTitle: "expect"
weight: 2617
---

 Replaces the statuses treated as success. <example><code>request |&gt; expect [ 200; 404 ]</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.expect&#32;<span>statuses&#32;request</span></span></code>
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
