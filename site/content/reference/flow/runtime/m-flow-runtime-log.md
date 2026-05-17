---
title: "Flow.Runtime.log"
linkTitle: "log"
type: docs
---

Writes a message through the ambient runtime logger.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.log&#32;<span>message</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `message` | <code>string</code> | The message to log. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that logs the message and returns unit. |

