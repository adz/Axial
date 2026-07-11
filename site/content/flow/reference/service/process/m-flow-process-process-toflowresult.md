---
title: "Flow.Process.toFlowResult"
linkTitle: "toFlowResult"
weight: 2501
---

 Converts a topology to Flow without interpreting stage success policies.
 <example><code>pipeline |&gt; Process.toFlowResult</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.toFlowResult&#32;<span>pipeline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pipeline` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |
