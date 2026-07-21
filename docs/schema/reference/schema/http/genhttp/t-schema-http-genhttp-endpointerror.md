---
title: "Schema.Http.GenHttp.EndpointError"
linkTitle: "EndpointError<error>"
weight: 1001
---

Distinguishes invalid request input from an expected application failure.

## Signature

<div class="fsdocs-usage">
<code>type EndpointError<'error></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |

## Union Cases

| Case | Description |
| --- | --- |
| `InvalidRequest` | The request could not be parsed into the declared trusted input. |
| `ApplicationError` | The application workflow failed with its typed error. |

## Remarks

Request operations create <code>InvalidRequest</code>; <code>EndpointFlow.run</code> wraps the application error channel as <code>ApplicationError</code>. <code>flowEndpoint</code> renders the two cases separately.
