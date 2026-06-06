---
title: "Check.takeOk"
linkTitle: "takeOk"
weight: 3003
type: docs
---

Takes the successful value from a result when it is <code>Ok</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.takeOk&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The result to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when the result is <code>Ok value</code>; otherwise <code>Error ()</code>. |
