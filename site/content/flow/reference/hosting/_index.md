---
title: ".NET Hosting"
weight: 500
type: docs
---

This page shows `Axial.Flow.Hosting` for .NET. `DotNetApp` owns Ctrl+C and process exit codes without requiring Generic Host. `Hosting.addApp` installs one root Flow application into Generic Host lifetime. `MicrosoftLogging` adapts MEL to the explicit `ILog` service, while `FiberLogging` reports fiber defects at the root edge. See the [.NET hosting guide](/flow/hosting/) for complete setup.

## Standalone

- [`Flow.Hosting.DotNetApp.run`](./m-flow-hosting-dotnetapp-run.md):  Runs a standalone application, translating Ctrl+C into coordinated stop and returning a process exit code.
- [`Flow.Hosting.DotNetApp.exitCode`](./m-flow-hosting-dotnetapp-exitcode.md): Maps a final application exit to the conventional standalone process exit codes.

## Generic Host

- [`Flow.Hosting.HostedAppOptions`](./t-flow-hosting-hostedappoptions.md): Controls how a root Flow application participates in Generic Host lifetime.
- [`Flow.Hosting.FlowHostedService`](./t-flow-hosting-flowhostedservice.md): Runs one root Flow application as a Microsoft Generic Host hosted service.
- [`Flow.Hosting.addApp`](./m-flow-hosting-hosting-addapp.md): Registers a root application that owns the Generic Host lifetime.
- [`Flow.Hosting.addAppWith`](./m-flow-hosting-hosting-addappwith.md): Registers a root application with explicit Generic Host completion options.

## Logging

- [`Flow.Hosting.MicrosoftLogging.create`](./m-flow-hosting-microsoftlogging-create.md): Creates an Axial logger backed by a supplied Microsoft logger.
- [`Flow.Hosting.MicrosoftLogging.fromFactory`](./m-flow-hosting-microsoftlogging-fromfactory.md): Creates an Axial logger with an explicit Microsoft logging category.
- [`Flow.Hosting.MicrosoftLogging.layer`](./m-flow-hosting-microsoftlogging-layer.md): Builds an Axial logger from a Microsoft logger factory supplied in the layer input.
- [`Flow.Hosting.FiberLogging.observer`](./m-flow-hosting-fiberlogging-observer.md): Logs fiber defects as errors and unobserved defects as critical entries.
- [`Flow.Hosting.FiberLogging.observe`](./m-flow-hosting-fiberlogging-observe.md): Installs fiber defect logging at the root application edge.
