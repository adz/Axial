---
title: "Node Hosting"
weight: 500
type: docs
---

This page shows the JavaScript-only `Axial.Flow.Hosting.Node` surface. `NodeApp` connects SIGINT/SIGTERM and `process.exitCode` to a root `App`; `NodeEnvironment.live` exposes `process.env` as the explicit `IEnvironmentVariables` service. See the [Node hosting guide](/flow/hosting/node/) for complete Fable setup.

## Node application

- [`Flow.Hosting.Node.NodeApp.arguments`](./m-flow-hosting-node-nodeapp-arguments.md): Gets command-line arguments after the Node executable and script path.
- [`Flow.Hosting.Node.NodeApp.start`](./m-flow-hosting-node-nodeapp-start.md):  Starts a Node application, translating SIGINT and SIGTERM into coordinated stop and publishing its exit code.
- [`Flow.Hosting.Node.NodeApp.run`](./m-flow-hosting-node-nodeapp-run.md): Starts a Node application and waits for its final exit.
- [`Flow.Hosting.Node.NodeApp.exitCode`](./m-flow-hosting-node-nodeapp-exitcode.md): Maps a final application exit to conventional Node process exit codes.

## Environment

- [`Flow.Hosting.Node.NodeEnvironment.live`](./p-flow-hosting-node-nodeenvironment-live.md): Creates an environment-variable service backed by <code>process.env</code>.
