---
title: "Flow.Http.DSL.POST"
linkTitle: "POST"
weight: 2602
type: docs
---

 Creates a POST request from an interpolated URL with encoded holes.
 <example><code>POST $"https://api.example.com/users" |&gt; jsonBody payload</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.POST&#32;<span>template</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `template` | <code><a href="https://learn.microsoft.com/dotnet/api/system.formattablestring">FormattableString</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
