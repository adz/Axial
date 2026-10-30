---
title: Layer
api:
  family: Axial.LayerModule
  sections:
    - id: create
      title: Create layers
      order: 10
      members: ["succeed", "fromFlow", "envWith"]
      facets:
        task: [create]
    - id: resources
      title: Manage resources
      order: 20
      members: ["addFinalizer", "acquireRelease", "pool"]
      facets:
        task: [resources]
    - id: transform
      title: Transform layers
      order: 30
      members: ["map", "mapError", "widenError"]
      facets:
        task: [transform]
    - id: combine
      title: Combine layers
      order: 40
      members: ["bind", "zip", "zipPar", "merge", "map2", "apply", "map3"]
      facets:
        task: [combine]
    - id: provide
      title: Provide environments
      order: 50
      members: ["provide"]
      facets:
        task: [provide]
---
# Layer

`Layer<'input, 'error, 'output>` builds an environment or service bundle from an input value, running inside a
`Scope` so acquired resources are finalized when the provided flow finishes. Read the
[composing layers guide](/layers/layers.html) for the `layer { }` provisioning walkthrough.

## Combinator inventory

| Function | Use |
| --- | --- |
| `Layer.succeed value` | A value that is already built |
| `Layer.envWith projection` | A value read from the layer's input |
| `Layer.fromFlow build` | Construction written as a flow: it can fail, read the input, and acquire scoped resources that live as long as the provided flow |
| `Layer.acquireRelease acquire release` | A resource built by another layer, released when the provided flow ends |
| `Layer.addFinalizer release` | Cleanup that runs when the provided flow ends |
| `Layer.map`, `Layer.mapError`, `Layer.widenError` | Change the output or the error type |
| `Layer.bind` | A step that depends on an earlier value, like `let!` |
| `Layer.zip` | Two layers in sequence |
| `Layer.zipPar`, `Layer.merge` | Two layers in parallel |
| `Layer.map2`, `Layer.map3` | Combine outputs without nested tuples |

## Composition rules

`Layer.zip` provisions left then right, sequentially. `Layer.zipPar` provisions both sides independently in parallel
and returns a tuple. `Layer.merge` is the layer-domain name for `zipPar`; prefer it when combining service bundles or
environment fragments. `Layer.merge` does not merge service contracts or synthesize a new environment type — it only
provisions both sides and returns their outputs, so build the final environment yourself.

Layer error types must match the flow error type. When provisioning steps use different errors, map them into one
startup error type with `Layer.mapError` before calling `Layer.provide`. A layer that cannot fail, such as
`EnvironmentVariables.layer`, has error type `Never`; `Layer.widenError` gives it any error type.

Parallel composition uses parent-owned child scopes, so when one parallel branch fails after another acquired
resources, the acquired resources are still released. If both parallel branches fail, Axial preserves both failures
as `Cause.Both (leftCause, rightCause)` rather than discarding one side.
