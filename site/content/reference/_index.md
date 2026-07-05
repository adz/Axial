---
title: Reference
weight: 30
type: docs
---


An alphabetical API dump answers "what exists" but not "where do I look first". This page is the generated API
reference for Axial core and the standard service packages, organised by the question you arrived with: writing a
workflow, describing a model, checking pure values, wiring dependencies, or reading an outcome.

Start with [`Flow`](./flow/) if you are writing application workflows. It is the central execution
type: a cold computation that reads `env`, returns a typed failure or success value, and preserves
interruption and defects. Use [`flow { }`](./flow/builders-flow/) for normal orchestration syntax.
Use [`Fiber`](./fiber/) when you need the handle returned by `Flow.fork`: it represents running child
work that can be joined or interrupted.

Use [`Schema`](./schema/) for domain-model boundaries: one declaration drives input parsing
([`Schema Interpreters`](./schema/interpreters/)), compiled JSON codecs ([`Codec`](./codec/)), JSON Schema
generation, and metadata inspection.

Use [`Check`](./check/) and [`Validation`](./validation/) before reaching for `Flow` when the code is
still pure. `Check` is for reusable boolean-like predicates; `Validation` is for accumulating
field-level diagnostics. Use [`Diagnostics`](./diagnostics/) when you need to inspect, merge, or
render those validation failures.

Use the service references when a workflow needs named dependencies. [`Service`](./service/)
documents nominal service contracts and provider-edge lookup, [`Layer`](./layer/) documents
provisioning, and [`Scope`](./scope/) documents cleanup ownership. Keep application dependencies
explicit in `env` and reserve [`Flow.Runtime`](./flow/runtime/) for closed executor mechanics.

Use [`Ref`](./ref/), [`STM`](./stm/), [`Schedule`](./schedule/), and [`Stream`](./stream/) for focused
runtime concerns: mutable references, transactional state, retry or repeat policy, and pull-based
streams. These modules are useful, but they are not the starting point for ordinary application
code.

Finally, understand the core model outcomes: [`Exit`](./exit/) is Axial's name for
`Result<'value, Cause<'error>>`, because it represents a completed workflow execution rather than an
ordinary domain result, and [`Cause`](./cause/) explains why a flow failed.
