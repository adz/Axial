---
title: "Flow.provide"
linkTitle: "provide"
weight: 2300
---

Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.provide&#32;<span>layer&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `layer` | <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'environment</span>&gt;</span></code> | The layer that builds the downstream environment. |
| `flow` | <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'environment,&#32;'error,&#32;'value</span>&gt;</span></code> | The flow to run with the provided environment. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'input,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that requires only the input environment of the layer. |

## Remarks


 This is the provisioning boundary for explicit services. It creates a fresh scope, builds the
 supplied layer inside that scope, runs the downstream flow with the built environment, and
 finalizes all acquired resources when the downstream flow completes or fails.
