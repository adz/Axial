---
title: "Schema.Http.AspNetCore.HttpEndpointEnv"
linkTitle: "HttpEndpointEnv<app>"
weight: 1000
type: docs
---

The request-scoped environment supplied to an ASP.NET Core endpoint Flow.

## Signature

<div class="fsdocs-usage">
<code>type HttpEndpointEnv<'app></code>
</div>

## Type Parameters

| Name |
| --- |
| `app` |

## Record Fields

| Field | Description |
| --- | --- |
| `App` | The application's explicit services and request-derived domain context. |
| `Request` | The native request, used by boundary operations in the adapter. |

## Remarks

The host factory supplies <code>App</code>; adapter request operations read <code>Request</code>. Keep application workflows typed against <code>&#39;app</code> and embed them with <code>EndpointFlow.run</code>.
