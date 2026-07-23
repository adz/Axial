---
title: "Schema.Path.fold"
linkTitle: "fold"
weight: 2207
type: docs
---

Folds over string keys and integer indexes without exposing a path-segment type.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Path.fold&#32;<span>keyFolder&#32;indexFolder&#32;state&#32;path</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `keyFolder` | <code><span>'a&#32;->&#32;string&#32;->&#32;'a</span></code> |  |
| `indexFolder` | <code><span>'a&#32;->&#32;int&#32;->&#32;'a</span></code> |  |
| `state` | <code>'a</code> |  |
| `path` | <code><a href="t-schema-path.md">Path</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>'a</code> |  |
