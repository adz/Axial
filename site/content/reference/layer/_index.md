---
title: "Layer"
weight: 150
type: docs
---

This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`. Use `layer { }` for application environment construction: plain `let!` is dependent and sequential, while sibling `and!` bindings use `Layer.merge` / `Layer.zipPar` for independent parallel provisioning.

## Core type

- [`Layer`](./t-layer.md):
 Represents a provisioning step that builds an explicit environment inside a scope.


## Builder

- [`layer`](./p-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.


## Module functions

- [`Layer.effect`](./m-layer-effect.md): Creates a layer from a raw effectful provisioning function.
- [`Layer.succeed`](./m-layer-succeed.md): Creates a layer that succeeds with a fixed output value.
- [`Layer.read`](./m-layer-read.md): Projects part of the input environment into the layer output.
- [`Layer.addFinalizer`](./m-layer-addfinalizer.md): Registers an asynchronous finalizer with the layer scope.
- [`Layer.acquireRelease`](./m-layer-acquirerelease.md): Acquires a resource and registers its release with the layer scope.
- [`Layer.map`](./m-layer-map.md): Maps the successful output of a layer.
- [`Layer.mapError`](./m-layer-maperror.md): Maps the typed provisioning failure of a layer.
- [`Layer.bind`](./m-layer-bind.md): Sequences layer provisioning with a dependent follow-up layer.
- [`Layer.zip`](./m-layer-zip.md): Builds two layers from the same input and scope and returns both outputs.
- [`Layer.zipPar`](./m-layer-zippar.md): Builds two independent layers in parallel and returns both outputs.
- [`Layer.merge`](./m-layer-merge.md): Merges two independent service layers in parallel.
- [`Layer.map2`](./m-layer-map2.md): Combines two layers with a mapping function.
- [`Layer.apply`](./m-layer-apply.md): Applies a layer-wrapped function to a layer-wrapped value.
- [`Layer.map3`](./m-layer-map3.md): Combines three layers with a mapping function.

## Flow integration

- [`Flow.provide`](./m-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
