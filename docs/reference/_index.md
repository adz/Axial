---
title: Reference
weight: 30
---

# API Reference

This page shows the generated API reference for FsFlow core and the standard capability packages.

Start with [`Flow`](./flow/) if you are writing application workflows. It is the central execution
type: a cold computation that reads `env`, returns a typed failure or success value, and preserves
interruption and defects. Use [`flow { }`](./flow/builders-flow/) for normal orchestration syntax.

Use [`Check`](./check/) and [`Validation`](./validation/) before reaching for `Flow` when the code is
still pure. `Check` is for reusable boolean-like predicates; `Validation` is for accumulating
field-level diagnostics. Use [`Diagnostics`](./diagnostics/) when you need to inspect, merge, or
render those validation failures.

Use the capability references when a workflow needs named dependencies. [`Capability`](./capability/)
documents the edge helpers and compatibility tokens around `env`; the `caps-*` sections document the
standard capability packages. Keep application dependencies in `env` and keep runtime-owned services
such as clock, logging, random, GUID generation, and environment-variable lookup behind runtime
helpers and overrides.

Use [`Ref`](./ref/), [`STM`](./stm/), [`Schedule`](./schedule/), and [`Stream`](./stream/) for focused
runtime concerns: mutable references, transactional state, retry/repeat policy, and pull-based
streams. These modules are useful, but they are not the starting point for ordinary application
code.
