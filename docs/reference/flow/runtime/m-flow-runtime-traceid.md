---
title: "Flow.Runtime.traceId"
linkTitle: "traceId"
weight: 2006
---

Reads the current runtime trace id annotation if one is present.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.traceId&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>string&#32;option</span></span>&gt;</span></code> | A flow that succeeds with the ambient <code>trace_id</code> value, if present. |

