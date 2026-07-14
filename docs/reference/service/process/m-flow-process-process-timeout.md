---
title: "Flow.Process.timeout"
linkTitle: "timeout"
weight: 2408
---

 Sets the maximum execution time for the complete process topology.
 <example><code>specification |&gt; Process.timeout (TimeSpan.FromSeconds 30.0)</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.timeout&#32;<span>after&#32;specification</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `specification` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |
