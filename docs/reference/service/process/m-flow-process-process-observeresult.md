---
title: "Flow.Process.observeResult"
linkTitle: "observeResult"
weight: 2503
---

 Converts a topology to Flow with an asynchronous observer and without interpreting stage success policies.
 <example><code>pipeline |&gt; Process.observeResult observer</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Process.Process.observeResult&#32;<span>observer&#32;pipeline</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `observer` | <code><span><a href="t-flow-process-processoutput.md">ProcessOutput</a>&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;unit&gt;</span></span></code> |  |
| `pipeline` | <code><a href="t-flow-process-pipeline.md">Pipeline</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;<a href="t-flow-process-processerror.md">ProcessError</a>,&#32;<a href="t-flow-process-processresult.md">ProcessResult</a></span>&gt;</span></code> |  |
