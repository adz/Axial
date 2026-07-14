---
title: "Flow.Process.Script.run"
linkTitle: "run"
weight: 2503
type: docs
---

 Runs a process workflow with live services, writes failures through the supplied console, and returns a host exit code.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Script.run&#32;<span>console&#32;workflow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `console` | <code><a href="../console/t-flow-console-iconsole.md">IConsole</a></code> |  |
| `workflow` | <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span><a href="/reference/Axial/axial-flow-process-scriptenvironment.html">ScriptEnvironment</a>,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>int</code> |  |
