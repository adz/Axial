---
title: "Flow.HttpClient.Response.create"
linkTitle: "create"
weight: 2405
---

 Creates a synthetic response transcript at an explicit start time, primarily for test fakes.
 <example><code>Response.create startedAt 200 """{"ok":true}"""</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.Response.create&#32;<span>startedAt&#32;status&#32;bodyText</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `startedAt` | <code><a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset">DateTimeOffset</a></code> |  |
| `status` | <code>int</code> |  |
| `bodyText` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httpresponse.md">HttpResponse</a></code> |  |
