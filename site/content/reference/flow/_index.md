---
title: "Flow"
weight: 10
type: docs
---

This page shows the `Flow<'env, 'error, 'value>` surface, the central workflow type in FsFlow. A flow is a cold description of work that reads an explicit environment, can fail with a typed error, and only starts when you call an execution member such as `workflow.ToTask(env)`, `workflow.ToValueTask(env)`, `workflow.ToAsync(env)`, or `workflow.RunSynchronously(env)`. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them.

Note that common extensions such as `Flow.Retry` and `Flow.Repeat` are available as soon as you `open FsFlow` because their modules are marked with `[<AutoOpen>]`.

## Core type

- [`Flow`](./t-flow.md):
 Represents a cold workflow that reads an environment, returns a typed result, and is executed
 explicitly through one of its execution members such as <code>ToTask</code>, <code>ToAsync</code>, or <code>RunSynchronously</code>.


## Fiber operations

- [`Flow.fork`](./m-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`Flow.join`](./m-flow-join.md): Waits for a fiber to complete and returns its successful value or typed failure.
- [`Flow.interrupt`](./m-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.

## Execution

- [`Flow.ToAsync`](./m-flow-toasync.md): Starts the workflow and returns an F# async handle that completes with the final exit.
- [`Flow.ToTask`](./m-flow-totask.md): Starts the workflow and returns a task handle that completes with the final exit.
- [`Flow.ToValueTask`](./m-flow-tovaluetask.md): Starts the workflow and returns a value-task handle that completes with the final exit.
- [`Flow.RunSynchronously`](./m-flow-runsynchronously.md): Starts the workflow and blocks until the final exit is available.

## Module functions

- [`Flow.ok`](./m-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.error`](./m-flow-error.md): Creates a failing synchronous flow.
- [`Flow.succeed`](./m-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.value`](./m-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.fail`](./m-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.fromResult`](./m-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.fromOption`](./m-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.fromValueOption`](./m-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`Flow.fromAsync`](./m-flow-fromasync.md): Creates a flow from a raw async operation.
- [`Flow.fromTask`](./m-flow-fromtask.md): Creates a flow from a raw task operation.
- [`Flow.fromValueTask`](./m-flow-fromvaluetask.md): Creates a flow from a raw value task operation.
- [`Flow.orElseFlow`](./m-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`Flow.env`](./m-flow-env.md): Reads the current environment as the successful flow value.
- [`Flow.read`](./m-flow-read.md): Projects one value from the current environment.
- [`Flow.map`](./m-flow-map.md): Transforms the successful value of a flow.
- [`Flow.bind`](./m-flow-bind.md): Sequences a dependent flow after a successful value.
- [`Flow.tap`](./m-flow-tap.md): Runs an effect on success and preserves the original value.
- [`Flow.tapError`](./m-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`Flow.mapError`](./m-flow-maperror.md): Maps the error value of a synchronous flow.
- [`Flow.catch`](./m-flow-catch.md): Catches exceptions raised during execution and maps them to a typed error.
- [`Flow.orElseWith`](./m-flow-orelsewith.md): Computes a fallback flow from the typed error when the source flow fails.
- [`Flow.orElse`](./m-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`Flow.zip`](./m-flow-zip.md): Runs two flows sequentially and combines their successful values into a tuple.
- [`Flow.map2`](./m-flow-map2.md): Combines two flows with a mapping function.
- [`Flow.map3`](./m-flow-map3.md): Combines three flows with a mapping function.
- [`Flow.apply`](./m-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`Flow.ignore`](./m-flow-ignore.md): Maps the successful value of a synchronous flow to <code>unit</code>.
- [`Flow.localEnv`](./m-flow-localenv.md): Runs a flow against an environment derived from the outer environment.
- [`Flow.provide`](./m-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
- [`Flow.delay`](./m-flow-delay.md): Defers flow construction until execution time.
- [`Flow.traverse`](./m-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.sequence`](./m-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Scoped resources

- [`Flow.addFinalizer`](./m-flow-addfinalizer.md): Registers an asynchronous finalizer with the current runtime scope.
- [`Flow.addDisposable`](./m-flow-adddisposable.md): Registers a disposable resource with the current runtime scope.
- [`Flow.addAsyncDisposable`](./m-flow-addasyncdisposable.md): Registers an asynchronously disposable resource with the current runtime scope.
- [`Flow.acquireRelease`](./m-flow-acquirerelease.md): Acquires a resource and registers its release with the current runtime scope.
- [`Flow.acquireReleaseWith`](./m-flow-acquirereleasewith.md): Acquires a resource, uses it, and always runs the release action.

## Parallel orchestration

- [`Flow.zipPar`](./m-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`Flow.race`](./m-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.

## Scheduling

- [`Flow.Retry`](./m-flow-retry.md): Retries a failing flow according to the supplied schedule.
- [`Flow.Repeat`](./m-flow-repeat.md): Repeats a successful flow according to the supplied schedule.
