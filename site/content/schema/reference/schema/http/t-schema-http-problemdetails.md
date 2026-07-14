---
title: "Schema.Http.ProblemDetails"
linkTitle: "ProblemDetails"
weight: 1100
type: docs
---

An RFC 9457 problem-details value carrying path-aware parse errors.

## Signature

<div class="fsdocs-usage">
<code>type ProblemDetails</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Type` |  |
| `Title` |  |
| `Status` |  |
| `Detail` |  |
| `Errors` |  |

## Remarks


 This is the shared error contract for schema-driven endpoints: every host adapter renders the same JSON body with
 media type <code>application/problem+json</code>, so clients handle one error shape regardless of the server behind it.
