---
title: For AI agents
description: High-signal Flow, services, resource, and hosting guidance for coding agents.
weight: 100
type: docs
---


Use this section for `Axial.Flow` and its service and hosting packages. Flow has no dependency on Schema or
ErrorHandling.

- Start with the smallest useful [`Flow`]({{< relref "/flow/reference/flow/t-flow-flow.md" >}}) alias and expand to `Flow<'env, 'error, 'value>` when both channels matter.
- Keep application and operational dependencies explicit in `'env`; reserve the runtime for executor mechanics.
- Use plain records for application environments and [`IHas<'service>`]({{< relref "/flow/reference/service/t-flow-ihas.md" >}}) when a reusable nominal service is worthwhile.
- Use [`Layer`]({{< relref "/flow/reference/layer/_index.md" >}}) and [`Flow.provide`]({{< relref "/flow/reference/layer/m-flow-flow-provide.md" >}}) to construct environments and own scoped resources.
- Treat exceptions as defects unless an `attempt*` constructor explicitly turns them into typed failures.
- Run finite roots with [`App.run`]({{< relref "/flow/reference/app/m-flow-app-run.md" >}}); use [`App.start`]({{< relref "/flow/reference/app/m-flow-app-start.md" >}}) when a .NET, Node, or browser owner needs an [`AppHandle`]({{< relref "/flow/reference/app/t-flow-apphandle.md" >}}).
- Keep Process and HttpClient effects behind `IProcess` and `IHttp`; clocks, randomness, environment variables, files,
  and console access remain separate explicit services.

Platform support is listed in [Packages and platforms]({{< relref "/flow/packages-and-platforms.md" >}}). For compact prompt context, load
[`/flow/llms.txt`](/flow/llms.txt).
