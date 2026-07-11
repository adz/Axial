---
title: "Flow.Process.pipe"
linkTitle: "pipe"
weight: 2401
---

 Connects the current stdout to the next command's stdin. <example><code>pipeline |&gt; Process.pipe next</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.pipe&#32;<span>next&#32;source</span></span></code>
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
