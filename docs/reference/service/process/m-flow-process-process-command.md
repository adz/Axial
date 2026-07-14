---
title: "Flow.Process.command"
linkTitle: "command"
weight: 2300
---

 Creates a runnable, safely tokenized one-command process specification.
 <example><code>Process.command "git" [ "status"; "--short" ] |&gt; Process.run</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.command&#32;<span>fileName&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fileName` | <code>string</code> |  |
| `arguments` | <code><span>string&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-process-processspec.md">ProcessSpec</a></code> |  |
