---
title: "Flow.Process.capture"
linkTitle: "capture"
weight: 2501
---

 Runs a process specification with complete stdout and stderr capture.
 <example><code>Process.command "dotnet" [ "--info" ] |&gt; Process.capture</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.capture&#32;<span>specification</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `specification` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |
