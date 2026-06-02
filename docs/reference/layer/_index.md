---
title: "Layer"
weight: 150
---

This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`.

## Core type

- [`Layer`](./t-layer.md): 
 Represents a provisioning step that builds an explicit environment inside a scope.
 

## Module functions

- [`Layer.effect`](./m-layer-effect.md): Creates a layer from a raw effectful provisioning function.
- [`Layer.succeed`](./m-layer-succeed.md): Creates a layer that succeeds with a fixed output value.
- [`Layer.read`](./m-layer-read.md): Projects part of the input environment into the layer output.
- [`Layer.map`](./m-layer-map.md): Maps the successful output of a layer.
- [`Layer.bind`](./m-layer-bind.md): Sequences layer provisioning with a dependent follow-up layer.
- [`Layer.zip`](./m-layer-zip.md): Builds two layers from the same input and scope and returns both outputs.

## Flow integration

- [`Flow.provide`](./m-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.

