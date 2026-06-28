---
title: "ErrorHandling.Result.require"
linkTitle: "require"
weight: 2201
---

Turns a boolean condition into a unit-success result with the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.require&#32;<span>condition&#32;failure</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `condition` | <code>bool</code> |  |
| `failure` | <code>'error</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;'error</span>&gt;</span></code> |  |
