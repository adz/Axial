---
title: "Flow.Process.pipeBoth"
linkTitle: "pipeBoth"
weight: 2402
type: docs
---

 Connects both stdout and stderr from the current final stage to the next command's stdin.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.pipeBoth&#32;<span>next&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `next` | <code><a href="t-flow-process-command.md">Command</a></code> |  |
| `source` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |
