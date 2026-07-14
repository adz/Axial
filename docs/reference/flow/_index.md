---
title: "Flow"
weight: 10
---

This page shows the Flow surface for cold workflow descriptions that only start when you call an execution member such as `workflow.ToTask(env)`, `workflow.ToValueTask(env)`, `workflow.ToAsync(env)`, or `workflow.RunSynchronously(env)`. The smallest useful signature is `Flow<'value>`: no environment and no typed failure. `Flow<'error, 'value>` adds typed failure with no environment; `EnvFlow<'env, 'value>` adds an environment with no typed failure; `ExnFlow`/`ExnEnvFlow` put recoverable exceptions in the typed error channel; the full `Flow<'env, 'error, 'value>` form carries both. Use this page as the API map for building fail-fast workflows with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reading dependencies from `env`; reshaping environments with `localEnv`; composing typed failures; and introducing concurrency with fibers, `zipPar`, or `race`. Reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them.

## Core type

- [`Flow`](./t-flow-flow.md):
 Represents a cold workflow that reads an environment, returns a typed result, and is executed
 explicitly through one of its execution members such as <code>ToTask</code>, <code>ToAsync</code>, or <code>RunSynchronously</code>.

- [`Flow`](./t-flow-flow.md):
- [`Flow`](./t-flow-flow.md):
- [`EnvFlow`](./t-flow-envflow.md): A flow that reads an environment and cannot fail with a typed error.
- [`ExnFlow`](./t-flow-exnflow.md): A flow that requires no environment and uses exceptions as recoverable typed errors.
- [`ExnEnvFlow`](./t-flow-exnenvflow.md): A flow that reads an environment and uses exceptions as recoverable typed errors.
- [`Flow.Never`](./t-flow-never.md):  Represents an error channel that cannot occur.

## Fiber operations

- [`Flow.fork`](./concurrency/m-flow-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`Flow.forkDetached`](./concurrency/m-flow-flow-forkdetached.md): Starts a flow in a new fiber that is deliberately never awaited.
- [`Flow.join`](./concurrency/m-flow-flow-join.md): Waits for a fiber to complete and returns its successful value or typed failure.
- [`Flow.interrupt`](./concurrency/m-flow-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.
- [`Flow.withFiberObserver`](./concurrency/m-flow-flow-withfiberobserver.md): Installs runtime fiber-lifecycle hooks for diagnostics and telemetry.

## Execution

- [`Flow.ToAsync`](./execution/m-flow-flow-toasync.md): Starts the workflow and returns an F# async handle that completes with the final exit.
- [`Flow.ToTask`](./execution/m-flow-flow-totask.md): Starts the workflow and returns a task handle that completes with the final exit.
- [`Flow.ToValueTask`](./execution/m-flow-flow-tovaluetask.md): Starts the workflow and returns a value-task handle that completes with the final exit.
- [`Flow.RunSynchronously`](./execution/m-flow-flow-runsynchronously.md): Starts the workflow and blocks until the final exit is available.

## Module functions

- [`Flow.ok`](./construction/m-flow-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.error`](./construction/m-flow-flow-error.md): Creates a failing synchronous flow.
- [`Flow.succeed`](./construction/m-flow-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.value`](./construction/m-flow-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.fail`](./construction/m-flow-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.fromResult`](./construction/m-flow-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.fromOption`](./construction/m-flow-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.fromValueOption`](./construction/m-flow-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`Flow.fromAsync`](./construction/m-flow-flow-fromasync.md): Creates a flow from a raw async operation.
- [`Flow.attemptAsync`](./construction/m-flow-flow-attemptasync.md): Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.fromTask`](./construction/m-flow-flow-fromtask.md): Creates a flow from a raw task operation.
- [`Flow.attemptTask`](./construction/m-flow-flow-attempttask.md): Creates a flow from a task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.fromValueTask`](./construction/m-flow-flow-fromvaluetask.md): Creates a flow from a raw value task operation.
- [`Flow.attemptValueTask`](./construction/m-flow-flow-attemptvaluetask.md): Creates a flow from a value task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.verify`](./composition/m-flow-flow-verify.md): Runs an environment-aware policy against an input value inside a workflow.
- [`Flow.orElseFlow`](./construction/m-flow-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`Flow.env`](./environment/m-flow-flow-env.md): Reads the current environment as the successful flow value.
- [`Flow.read`](./environment/m-flow-flow-read.md): Projects one value from the current environment.
- [`Flow.map`](./composition/m-flow-flow-map.md): Transforms the successful value of a flow.
- [`Flow.bind`](./composition/m-flow-flow-bind.md): Sequences a dependent flow after a successful value.
- [`Flow.tap`](./composition/m-flow-flow-tap.md): Runs an effect on success and preserves the original value.
- [`Flow.tapError`](./composition/m-flow-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`Flow.mapError`](./composition/m-flow-flow-maperror.md): Maps the error value of a synchronous flow.
- [`Flow.tracedError`](./composition/m-flow-flow-tracederror.md): Attaches diagnostic trace text to any failure cause of the flow.
- [`Flow.catch`](./composition/m-flow-flow-catch.md): Catches exceptions raised during execution and simple defect outcomes, then maps them to a typed error.
- [`Flow.orElseWith`](./composition/m-flow-flow-orelsewith.md): Computes a fallback flow from the typed error when the source flow fails.
- [`Flow.orElse`](./composition/m-flow-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`Flow.zip`](./composition/m-flow-flow-zip.md): Runs two flows sequentially and combines their successful values into a tuple.
- [`Flow.map2`](./composition/m-flow-flow-map2.md): Combines two flows with a mapping function.
- [`Flow.map3`](./composition/m-flow-flow-map3.md): Combines three flows with a mapping function.
- [`Flow.apply`](./composition/m-flow-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`Flow.ignore`](./composition/m-flow-flow-ignore.md): Maps the successful value of a synchronous flow to <code>unit</code>.
- [`Flow.localEnv`](./environment/m-flow-flow-localenv.md): Runs a flow against an environment derived from the outer environment.
- [`Flow.provide`](./environment/m-flow-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
- [`Flow.delay`](./construction/m-flow-flow-delay.md): Defers flow construction until execution time.
- [`Flow.traverse`](./composition/m-flow-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.sequence`](./composition/m-flow-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Policies

- [`Flow.Policy`](./t-flow-policy.md): Constructors and combinators for environment-aware workflow requirements.
- [`Flow.Policy.withError`](./m-flow-policy-witherror.md): Lifts a pure result-returning function and replaces any error with a fixed workflow error.
- [`Flow.Policy.context`](./m-flow-policy-context.md): Lifts an environment-aware result-returning function and maps its error into the workflow error type.
- [`Flow.Policy.pass`](./p-flow-policy-pass.md): A policy that returns the input unchanged.
- [`Flow.Policy.compose`](./m-flow-policy-compose.md): Composes two policies left to right.
- [`Flow.Policy.optional`](./m-flow-policy-optional.md): Runs a policy only when the environment predicate is true; otherwise returns the input unchanged.

## Scoped resources

- [`Flow.addFinalizer`](./resources/m-flow-flow-addfinalizer.md): Registers an asynchronous finalizer with the current runtime scope.
- [`Flow.addDisposable`](./resources/m-flow-flow-adddisposable.md): Registers a disposable resource with the current runtime scope.
- [`Flow.addAsyncDisposable`](./resources/m-flow-flow-addasyncdisposable.md): Registers an asynchronously disposable resource with the current runtime scope.
- [`Flow.acquireRelease`](./resources/m-flow-flow-acquirerelease.md): Acquires a resource and registers its release with the current runtime scope.
- [`Flow.acquireReleaseWith`](./resources/m-flow-flow-acquirereleasewith.md): Acquires a resource, uses it, and always runs the release action.

## Parallel orchestration

- [`Flow.zipPar`](./concurrency/m-flow-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`Flow.race`](./concurrency/m-flow-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.

## Scheduling

- [`Flow.Schedule.retry`](./scheduling/m-flow-schedule-retry.md): Retries a failing flow according to the supplied schedule.
- [`Flow.Schedule.repeat`](./scheduling/m-flow-schedule-repeat.md): Repeats a successful flow according to the supplied schedule.
