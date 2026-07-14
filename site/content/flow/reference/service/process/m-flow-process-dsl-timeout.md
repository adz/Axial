---
title: "Flow.Process.DSL.timeout"
linkTitle: "timeout"
weight: 2813
type: docs
---

 Sets the maximum execution time for a command or specification.
 <example><code>cmd $"service-device" |&gt; timeout (TimeSpan.FromSeconds 30.0) |&gt; capture</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.DSL.timeout&#32;<span>after&#32;source</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `after` | <code><a href="https://learn.microsoft.com/dotnet/api/system.timespan">TimeSpan</a></code> |  |
| `source` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |
