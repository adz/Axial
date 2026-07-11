---
title: "Flow.Http.DSL.secretQuery"
linkTitle: "secretQuery"
weight: 2608
---

 Appends a redacted query parameter. <example><code>request |&gt; secretQuery "api_key" key</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.secretQuery&#32;<span>name&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>'a</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
