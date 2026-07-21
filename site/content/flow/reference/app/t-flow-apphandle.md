---
title: "Flow.AppHandle"
linkTitle: "AppHandle<error, value>"
weight: 1001
type: docs
---


 Owns one running root workflow and provides coordinated application shutdown.


## Signature

<div class="fsdocs-usage">
<code>type AppHandle<'error, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `error` |
| `value` |

## Remarks


 Calling <a href="t-flow-apphandle.md#Stop">AppHandle.Stop</a> more than once is safe. Every caller observes the same
 final <a href="../exit/t-flow-exit.md">Exit</a> after the root scope has closed. Disposing the handle requests stop but
 cannot wait for asynchronous finalizers; await <code>Stop()</code> or <code>Completion</code> when cleanup must finish before
 the surrounding process or host exits.
