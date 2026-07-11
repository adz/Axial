---
title: "Flow.Process.execute"
linkTitle: "execute"
weight: 2505
---

 Creates and runs one command with default capture policy.
 <example><code>Process.execute "dotnet" [ "--version" ]</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.execute&#32;<span>fileName&#32;arguments</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fileName` | <code>string</code> |  |
| `arguments` | <code><span>string&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |
