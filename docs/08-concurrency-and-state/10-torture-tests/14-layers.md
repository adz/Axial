---
title: Layers
description: "Layers provision an environment from resources in sequence and in parallel, with failures during provisioning and interruption during use."
---

# Layers

Each round provisions an environment from [layers](/layers/layers.html). Some resources fail while they are acquired,
and the flow that uses the environment may fail or be interrupted.

Run it with `dotnet run --project examples/Axial.TortureTest -- layers 100`.

## What it does

- Each resource layer is built with `Layer.acquireRelease` and fails one time in twenty, through `Layer.fromAsync`, or
  on .NET `Layer.fromTask` or `Layer.fromValueTask`.
- Two resources are provisioned in sequence with `Layer.zip`, a pool of three with `Layer.pool` in parallel with
  `Layer.zipPar`, and the region comes from the input environment with `Layer.envWith`.
- The flow reads the environment with `Flow.env` and `Flow.envWith`, uses the pool, and may fail. It runs under
  `Layer.provide` and is interrupted about a third of the time.

{{< snippet id="torture-layers" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every provisioned resource was released exactly once | A layer's resources are released when the flow ends, and a failure part way through provisioning releases what was already acquired. |
| The provisioned environment was built from its layers | The flow sees the values its layers produced, or fails with a provisioning or work failure, or is interrupted. |
| A layer provided to a flow ran it | `Layer.provide` runs the flow with the layer's output. |
