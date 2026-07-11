---
title: "Flow.Process.toFlow"
linkTitle: "toFlow"
weight: 2500
type: docs
---

 Converts a topology to Flow and fails on the first unsuccessful stage.
 <example><code>pipeline |&gt; Process.toFlow</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.toFlow&#32;<span>pipeline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `pipeline` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |
