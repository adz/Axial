---
title: "Check.takeError"
linkTitle: "takeError"
weight: 3004
---

Takes the error value from a result when it is <code>Error</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Check.takeError&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The result to unwrap. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'error&gt;</span></code> | <code>Ok error</code> when the result is <code>Error error</code>; otherwise <code>Error ()</code>. |
