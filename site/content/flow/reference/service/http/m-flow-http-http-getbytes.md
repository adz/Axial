---
title: "Flow.Http.getBytes"
linkTitle: "getBytes"
weight: 2512
type: docs
---

 Sends a GET request and returns the body bytes, mirroring <c>HttpClient.GetByteArrayAsync</c>.
 <example><code>Http.getBytes "https://example.com/logo.png"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.Http.getBytes&#32;<span>url</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `url` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-http-httperror.md">HttpError</a>,&#32;<span>byte&#32;array</span></span>&gt;</span></code> |  |
