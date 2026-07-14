---
title: "Flow.Process.environment"
linkTitle: "environment"
weight: 2304
type: docs
---

 Sets an environment override. <example><code>command |&gt; Process.environment "CI" "true"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.environment&#32;<span>name&#32;value&#32;specification</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> |  |
| `value` | <code>string</code> |  |
| `specification` | <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |
