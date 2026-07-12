---
title: "Flow.Http.live"
linkTitle: "live"
weight: 2700
---

 Creates a live HTTP service backed by an explicit clock and <see cref="T:System.Net.Http.HttpClient" />.
 <example><code>Http.live Clock.live (new HttpClient())</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.live&#32;<span>clock&#32;client</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `clock` | <code><a href="../core/t-flow-platformservice-iclock.md">IClock</a></code> |  |
| `client` | <code><a href="https://learn.microsoft.com/dotnet/api/system.net.http.httpclient">HttpClient</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-ihttp.md">IHttp</a></code> |  |
