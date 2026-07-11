---
title: "Flow.Http.DSL.fetchBytes"
linkTitle: "fetchBytes"
weight: 2621
type: docs
---

 Sends the request and returns the body bytes. <example><code>GET $"{root}/logo.png" |&gt; fetchBytes</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.fetchBytes&#32;<span>request</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `request` | <code><a href="t-flow-http-httprequest.md">HttpRequest</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<span>byte&#32;array</span></span>&gt;</span></code> |  |
