---
title: "Environment"
---

This page shows the helpers that read, reshape, and provide explicit environments for flows.

- [`Flow.Flow.env`](./m-flow-flow-env.md): Reads the current environment as the successful flow value.
- [`Flow.Flow.read`](./m-flow-flow-read.md): Projects one value from the current environment.
- [`Flow.Flow.localEnv`](./m-flow-flow-localenv.md): Runs a flow against an environment derived from the outer environment.
- [`Flow.Flow.provide`](./m-flow-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
