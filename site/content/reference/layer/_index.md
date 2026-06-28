---
title: "Layer"
weight: 150
type: docs
---

This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`. Use `layer { }` for application environment construction: plain `let!` is dependent and sequential, while sibling `and!` bindings use `Layer.merge` / `Layer.zipPar` for independent parallel provisioning.

## Core type

- [`Flow.Layer`](./t-flow-layer.md):
 Represents a provisioning step that builds an explicit environment inside a scope.


## Builder

- [`layer`](./p-flow--layer.md): The <code>layer { }</code> computation expression for provisioning explicit service environments.

## Module functions

- [`Flow.Layer.fromAsync`](./m-flow-layer-fromasync.md): Creates a layer from a raw async provisioning function.
- [`Flow.Layer.fromTask`](./m-flow-layer-fromtask.md): Creates a layer from a raw task provisioning function.
- [`Flow.Layer.fromValueTask`](./m-flow-layer-fromvaluetask.md): Creates a layer from a raw value task provisioning function.
- [`Flow.Layer.succeed`](./m-flow-layer-succeed.md): Creates a layer that succeeds with a fixed output value.
- [`Flow.Layer.read`](./m-flow-layer-read.md): Projects part of the input environment into the layer output.
- [`Flow.Layer.addFinalizer`](./m-flow-layer-addfinalizer.md): Registers an asynchronous finalizer with the layer scope.
- [`Flow.Layer.acquireRelease`](./m-flow-layer-acquirerelease.md): Acquires a resource and registers its release with the layer scope.
- [`Flow.Layer.map`](./m-flow-layer-map.md): Maps the successful output of a layer.
- [`Flow.Layer.mapError`](./m-flow-layer-maperror.md): Maps the typed provisioning failure of a layer.
- [`Flow.Layer.bind`](./m-flow-layer-bind.md): Sequences layer provisioning with a dependent follow-up layer.
- [`Flow.Layer.zip`](./m-flow-layer-zip.md): Builds two layers from the same input and scope and returns both outputs.
- [`Flow.Layer.zipPar`](./m-flow-layer-zippar.md): Builds two independent layers in parallel and returns both outputs.
- [`Flow.Layer.merge`](./m-flow-layer-merge.md): Merges two independent service layers in parallel.
- [`Flow.Layer.map2`](./m-flow-layer-map2.md): Combines two layers with a mapping function.
- [`Flow.Layer.apply`](./m-flow-layer-apply.md): Applies a layer-wrapped function to a layer-wrapped value.
- [`Flow.Layer.map3`](./m-flow-layer-map3.md): Combines three layers with a mapping function.

## Flow integration

- [`Flow.Flow.provide`](./m-flow-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
