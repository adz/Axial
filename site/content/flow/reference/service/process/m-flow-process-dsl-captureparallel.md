---
title: "Flow.Process.DSL.captureParallel"
linkTitle: "captureParallel"
weight: 2821
---

 Captures commands concurrently with a fixed upper bound while preserving input order.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.DSL.captureParallel&#32;<span>maximumConcurrency&#32;commands</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximumConcurrency` | <code>int</code> |  |
| `commands` | <code><span><a href="t-flow-process-command.md">Command</a>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'a,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<span><a href="t-flow-process-processresult.md">ProcessResult</a>&#32;list</span></span>&gt;</span></code> |  |
