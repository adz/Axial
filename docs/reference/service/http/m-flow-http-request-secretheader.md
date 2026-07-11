---
title: "Flow.Http.Request.secretHeader"
linkTitle: "secretHeader"
weight: 2304
---

 Appends a header whose value is replaced with <c>***</c> in plans and error transcripts.
 <example><code>request |&gt; Request.secretHeader "X-Api-Key" key</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Request.secretHeader&#32;<span>name&#32;value&#32;request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>string</code> |  |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |
