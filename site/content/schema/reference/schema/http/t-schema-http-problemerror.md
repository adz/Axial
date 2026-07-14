---
title: "Schema.Http.ProblemError"
linkTitle: "ProblemError"
weight: 1101
type: docs
---

One boundary error: a JSON pointer into the request body plus a rendered message.

## Signature

<div class="fsdocs-usage">
<code>type ProblemError</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Pointer` | RFC 6901 JSON pointer to the offending value; <code>&quot;&quot;</code> points at the whole document. |
| `Message` | The rendered error message. |
