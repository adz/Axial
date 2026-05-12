---
title: "Flow"
weight: 10
type: docs
---

This page shows the source-documented `Flow` surface: the core type and module functions.

## Core type

- [`FsFlow.Flow`](./t-flow.md): Represents a cold workflow that reads an environment, returns a typed result, and is executed
 explicitly through `Flow.run`.

## Module functions

- [`FsFlow.Flow.run`](./m-flow-run.md): Executes a flow with the provided environment and the default cancellation token.
- [`FsFlow.Flow.ok`](./m-flow-ok.md): Creates a successful synchronous flow.
- [`FsFlow.Flow.error`](./m-flow-error.md): Creates a failing synchronous flow.
- [`FsFlow.Flow.succeed`](./m-flow-succeed.md): Alias for `ok` that reads well in some call sites.
- [`FsFlow.Flow.value`](./m-flow-value.md): Alias for `ok` that reads well in some call sites.
- [`FsFlow.Flow.fail`](./m-flow-fail.md): Alias for `error` that reads well in some call sites.
- [`FsFlow.Flow.fromResult`](./m-flow-fromresult.md): Lifts a `Result` into a synchronous flow.
- [`FsFlow.Flow.fromOption`](./m-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`FsFlow.Flow.fromValueOption`](./m-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`FsFlow.Flow.orElseFlow`](./m-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`FsFlow.Flow.env`](./m-flow-env.md): Reads the current environment as the flow value.
- [`FsFlow.Flow.read`](./m-flow-read.md): Projects a value from the current environment.
- [`FsFlow.Flow.map`](./m-flow-map.md): Maps the successful value of a synchronous flow.
- [`FsFlow.Flow.bind`](./m-flow-bind.md): Sequences a synchronous continuation after a successful value.
- [`FsFlow.Flow.tap`](./m-flow-tap.md): Runs a synchronous side effect on success and preserves the original value.
- [`FsFlow.Flow.tapError`](./m-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`FsFlow.Flow.mapError`](./m-flow-maperror.md): Maps the error value of a synchronous flow.
- [`FsFlow.Flow.catch`](./m-flow-catch.md): Catches exceptions raised during execution and maps them to a typed error.
- [`FsFlow.Flow.orElseWith`](./m-flow-orelsewith.md): Falls back to another flow when the source flow fails.
- [`FsFlow.Flow.orElse`](./m-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`FsFlow.Flow.zip`](./m-flow-zip.md): Combines two flows into a tuple of their values.
- [`FsFlow.Flow.map2`](./m-flow-map2.md): Combines two flows with a mapping function.
- [`FsFlow.Flow.map3`](./m-flow-map3.md): Combines three flows with a mapping function.
- [`FsFlow.Flow.apply`](./m-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`FsFlow.Flow.ignore`](./m-flow-ignore.md): Maps the successful value of a synchronous flow to `unit`.
- [`FsFlow.Flow.localEnv`](./m-flow-localenv.md): Transforms the environment before running the flow.
- [`FsFlow.Flow.provideLayer`](./m-flow-providelayer.md): Provides a derived environment from a layer flow to a downstream flow.
- [`FsFlow.Flow.delay`](./m-flow-delay.md): Defers flow construction until execution time.
- [`FsFlow.Flow.traverse`](./m-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`FsFlow.Flow.sequence`](./m-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Concurrency

- [`FsFlow.Fiber`](./t-fiber.md): Represents a handle to a running workflow.
- [`FsFlow.Flow.fork`](./m-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`FsFlow.Flow.join`](./m-flow-join.md): Waits for a fiber to complete and returns its final outcome.
- [`FsFlow.Flow.interrupt`](./m-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.

## Parallel orchestration

- [`FsFlow.Flow.zipPar`](./m-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`FsFlow.Flow.race`](./m-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.

