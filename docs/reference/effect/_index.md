---
title: "Effect"
weight: 50
---

This page shows the `Effect<'value, 'error>` shape and the `EffectFlow` module. An effect is the deferred execution handle returned by `Flow.run`; on .NET it is a `ValueTask<Exit<'v, 'e>>` and on Fable it is an `Async<Exit<'v, 'e>>`. Use the `EffectFlow` functions for low-level algebra and for bridging between the unified flow surface and platform-native async primitives.

## Core type

- [`Effect`](./t-effect.md):
 Represents the portable execution shape used by the unified <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>.


## Module functions

- [`EffectFlow.ofValue`](./m-effectflow-ofvalue.md):
- [`EffectFlow.ofError`](./m-effectflow-oferror.md):
- [`EffectFlow.ofExit`](./m-effectflow-ofexit.md):
- [`EffectFlow.ofCause`](./m-effectflow-ofcause.md):
- [`EffectFlow.ofDie`](./m-effectflow-ofdie.md):
- [`EffectFlow.ofInterrupt`](./m-effectflow-ofinterrupt.md):
- [`EffectFlow.ofResult`](./m-effectflow-ofresult.md):
- [`EffectFlow.fold`](./m-effectflow-fold.md):
- [`EffectFlow.mapBoth`](./m-effectflow-mapboth.md):
