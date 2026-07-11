---
title: "Flow.Http.DSL.timeout"
linkTitle: "timeout"
weight: 2612
---

 Sets a per-request timeout. <example><code>request |&gt; timeout (TimeSpan.FromSeconds 5.0)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.timeout&#32;<span>value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
