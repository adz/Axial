---
title: "Flow.Process.DSL.pipeTo"
linkTitle: "pipeTo"
weight: 2804
---

 Connects stdout from a command or pipeline to the next command's stdin.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.DSL.pipeTo&#32;<span>next&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `next` | <code><a href="t-flow-process-command.md">Command</a></code> |  |
| `source` | <code>^a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |
