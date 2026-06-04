---
title: "Environment"
type: docs
---

This page shows the helpers that read, reshape, and provide explicit environments for flows.

- [`Flow.env`](./m-flow-env.md): Reads the current environment as the successful flow value.
- [`Flow.read`](./m-flow-read.md): Projects one value from the current environment.
- [`Flow.localEnv`](./m-flow-localenv.md): Runs a flow against an environment derived from the outer environment.
- [`Flow.provide`](./m-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
