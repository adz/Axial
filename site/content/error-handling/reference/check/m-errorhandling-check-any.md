---
title: "ErrorHandling.Check.any"
linkTitle: "any"
weight: 2101
type: docs
---

Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.any&#32;<span>checks&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="t-errorhandling-check.md">Check</a>&lt;'value&gt;</span>&#32;list</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
