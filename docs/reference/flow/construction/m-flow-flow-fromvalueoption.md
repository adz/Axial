---
title: "Flow.Flow.fromValueOption"
linkTitle: "fromValueOption"
weight: 2307
---

Lifts a value option into a synchronous flow with the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.fromValueOption&#32;<span>error&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The error to return if the value option is <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpvalueoption-1-valuenone">ValueNone</a>. |
| `value` | <code><span>'value&#32;voption</span></code> | The value option to lift. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a> that succeeds with the option&#39;s value or fails with the provided error. |
