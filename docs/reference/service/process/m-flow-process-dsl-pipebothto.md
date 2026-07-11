---
title: "Flow.Process.DSL.pipeBothTo"
linkTitle: "pipeBothTo"
weight: 2805
---

 Connects both stdout and stderr from the current final stage to the next command.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.DSL.pipeBothTo&#32;<span>next&#32;source</span></span></code>
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
