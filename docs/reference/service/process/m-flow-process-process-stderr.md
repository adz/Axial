---
title: "Flow.Process.stderr"
linkTitle: "stderr"
weight: 2406
---

 Configures combined stderr handling. <example><code>pipeline |&gt; Process.stderr (OutputTarget.CaptureTail 65536)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.stderr&#32;<span>destination&#32;pipeline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `destination` | <code><a href="t-flow-process-outputtarget.md">OutputTarget</a></code> |  |
| `pipeline` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |
