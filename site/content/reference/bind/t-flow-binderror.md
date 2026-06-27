---
title: "Flow.BindError"
linkTitle: "BindError<env, error, value>"
weight: 1000
type: docs
---


 A marker that adapts a source error before <code>flow { }</code> binds it.


## Signature

<div class="fsdocs-usage">
<code>type BindError<'env, 'error, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `env` |
| `error` |
| `value` |

## Remarks


 Use <code>Bind.error</code> for sources that fail with missingness or <code>unit</code>.
 Use <code>Bind.mapError</code> for sources that already carry a meaningful error.
