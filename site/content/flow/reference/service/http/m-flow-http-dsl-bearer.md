---
title: "Flow.Http.DSL.bearer"
linkTitle: "bearer"
weight: 2610
type: docs
---

 Adds a redacted bearer-token Authorization header. <example><code>request |&gt; bearer token</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.bearer&#32;<span>token&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `token` | <code>string</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
