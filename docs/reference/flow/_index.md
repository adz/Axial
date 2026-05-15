---
title: "Flow"
---

This page shows the `Flow<'env, 'error, 'value>` surface, the central workflow type in FsFlow. A flow is a cold description of work that reads an explicit environment, can fail with a typed error, and only runs when you call an execution function such as `Flow.run`. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for runtime helpers and parallel orchestration only at the boundary where the workflow actually needs them.

## Core type

- [`Flow`](./t-flow-3.md): 
 Represents a cold workflow that reads an environment, returns a typed result, and is executed
 explicitly through <code>Flow.run</code>.
 

## Module functions

- [`Flow.run`](./m-flow-run.md): Executes a flow with the provided environment and the default cancellation token.
- [`Flow.ok`](./m-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.error`](./m-flow-error.md): Creates a failing synchronous flow.
- [`Flow.succeed`](./m-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.value`](./m-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.fail`](./m-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.fromResult`](./m-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.fromOption`](./m-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.fromValueOption`](./m-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
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
- [`Flow.provideLayer`](./m-flow-providelayer.md): Runs a layer flow first, then runs a downstream flow with the layer&#39;s output as its environment.
- [`Flow.delay`](./m-flow-delay.md): Defers flow construction until execution time.
- [`Flow.traverse`](./m-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.sequence`](./m-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Concurrency

- [`Fiber`](./t-fiber-2.md): 
 Represents a handle to a running workflow.
 
- [`Flow.fork`](./m-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`Flow.join`](./m-flow-join.md): Waits for a fiber to complete and returns its final outcome.
- [`Flow.interrupt`](./m-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.

## Parallel orchestration

- [`Flow.zipPar`](./m-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`Flow.race`](./m-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.

