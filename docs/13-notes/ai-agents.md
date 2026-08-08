---
title: For AI agents
description: High-signal Flow, services, resource, and hosting guidance for coding agents.
weight: 100
---

# For AI agents

Use this section for `Axial` and its service and hosting packages. Flow depends on no other Axial
package — not Schema, not Result — though its builder lifts a plain F# `Result` when you have one.

- Start with the smallest useful `Flow` alias and expand to `Flow<'env, 'error, 'value>` when both channels matter.
- Keep application and operational dependencies explicit in `'env`; reserve the runtime for executor mechanics.
- Use plain records for application environments and `IHas<'service>` when a reusable nominal service is worthwhile.
- Use `Layer` and `Flow.provide` to construct environments and own scoped resources.
- Treat exceptions as defects unless an `attempt*` constructor explicitly turns them into typed failures.
- Run finite roots with `App.run`; use `App.start` when a .NET, Node, or browser owner needs an `AppHandle`.
- Keep Process and HttpClient effects behind `IProcess` and `IHttp`; clocks, randomness, environment variables, files,
  and console access remain separate explicit services.

Platform support is listed in [Packages and platforms](/notes/packages-and-platforms.html). For compact prompt context, load
[`/llms.txt`](/llms.txt).
