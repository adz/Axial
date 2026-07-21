---
title: "Flow.HttpClient.DSL.GET"
linkTitle: "GET"
weight: 2600
---

 Creates a GET request from an interpolated URL. Every hole is URL-encoded as one value.
 <example><code>GET $"https://api.example.com/users/{userId}"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.GET&#32;<span>template</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `template` | <code><a href="https://learn.microsoft.com/dotnet/api/system.formattablestring">FormattableString</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-httprequest.md">HttpRequest</a></code> |  |
