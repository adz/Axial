---
title: "Flow.Process.stderr"
linkTitle: "stderr"
weight: 2405
type: docs
---

 Configures combined stderr handling. <example><code>specification |&gt; Process.stderr (OutputTarget.CaptureTail 65536)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.stderr&#32;<span>destination&#32;specification</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `destination` | <code><a href="t-flow-process-outputtarget.md">OutputTarget</a></code> |  |
| `specification` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |
