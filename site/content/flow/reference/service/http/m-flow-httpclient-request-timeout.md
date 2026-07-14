---
title: "Flow.HttpClient.Request.timeout"
linkTitle: "timeout"
weight: 2310
type: docs
---

 Sets a per-request timeout enforced by the live service.
 <example><code>request |&gt; Request.timeout (TimeSpan.FromSeconds 5.0)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Request.timeout&#32;<span>value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `request` | <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
