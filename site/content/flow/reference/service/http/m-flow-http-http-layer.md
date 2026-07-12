---
title: "Flow.Http.layer"
linkTitle: "layer"
weight: 2701
type: docs
---

 Builds a live HTTP service from an explicit clock as a layer.
 <example><code>Http.layer Clock.live (new HttpClient())</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.layer&#32;<span>clock&#32;client</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `clock` | <code><a href="../core/t-flow-platformservice-iclock.md">IClock</a></code> |  |
| `client` | <code><a href="https://learn.microsoft.com/dotnet/api/system.net.http.httpclient">HttpClient</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../layer/t-flow-layer.md">Layer</a>&lt;<span>unit,&#32;<a href="../../flow/t-flow-never.md">Never</a>,&#32;<a href="t-flow-http-ihttp.md">IHttp</a></span>&gt;</span></code> |  |
