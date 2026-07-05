---
title: "Flow"
weight: 10
type: docs
---

This page shows the Flow surface for cold workflow descriptions that only start when you call an execution member such as `workflow.ToTask(env)`, `workflow.ToValueTask(env)`, `workflow.ToAsync(env)`, or `workflow.RunSynchronously(env)`. Start with the smallest useful signature: `Flow<'value>` means no environment and no typed failure, `Flow<'error, 'value>` means no environment with typed failure, `EnvFlow<'env, 'value>` means environment with no typed failure, and `ExnFlow`/`ExnEnvFlow` put recoverable exceptions in the typed error channel. Use the full `Flow<'env, 'error, 'value>` form when a workflow needs both environment and typed failure channels. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them.

## Core type

- [`Flow.Flow`](./t-flow-flow.md):
 Represents a cold workflow that reads an environment, returns a typed result, and is executed
 explicitly through one of its execution members such as <code>ToTask</code>, <code>ToAsync</code>, or <code>RunSynchronously</code>.

- [`Flow.Flow`](./t-flow-flow.md):
- [`Flow.Flow`](./t-flow-flow.md):
- [`EnvFlow`](./t-flow-envflow.md): A flow that reads an environment and cannot fail with a typed error.
- [`ExnFlow`](./t-flow-exnflow.md): A flow that requires no environment and uses exceptions as recoverable typed errors.
- [`ExnEnvFlow`](./t-flow-exnenvflow.md): A flow that reads an environment and uses exceptions as recoverable typed errors.
- [`Flow.Never`](./t-flow-never.md):  Represents an error channel that cannot occur.

## Fiber operations

- [`Flow.Flow.fork`](./concurrency/m-flow-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`Flow.Flow.join`](./concurrency/m-flow-flow-join.md): Waits for a fiber to complete and returns its successful value or typed failure.
- [`Flow.Flow.interrupt`](./concurrency/m-flow-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.

## Execution

- [`Flow.Flow.ToAsync`](./execution/m-flow-flow-toasync.md): Starts the workflow and returns an F# async handle that completes with the final exit.
- [`Flow.Flow.ToTask`](./execution/m-flow-flow-totask.md): Starts the workflow and returns a task handle that completes with the final exit.
- [`Flow.Flow.ToValueTask`](./execution/m-flow-flow-tovaluetask.md): Starts the workflow and returns a value-task handle that completes with the final exit.
- [`Flow.Flow.RunSynchronously`](./execution/m-flow-flow-runsynchronously.md): Starts the workflow and blocks until the final exit is available.

## Module functions

- [`Flow.Flow.ok`](./construction/m-flow-flow-ok.md): Creates a successful synchronous flow.
- [`Flow.Flow.error`](./construction/m-flow-flow-error.md): Creates a failing synchronous flow.
- [`Flow.Flow.succeed`](./construction/m-flow-flow-succeed.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.Flow.value`](./construction/m-flow-flow-value.md): Alias for <code>ok</code> that reads well in some call sites.
- [`Flow.Flow.fail`](./construction/m-flow-flow-fail.md): Alias for <code>error</code> that reads well in some call sites.
- [`Flow.Flow.fromResult`](./construction/m-flow-flow-fromresult.md): Lifts a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> into a synchronous flow.
- [`Flow.Flow.fromOption`](./construction/m-flow-flow-fromoption.md): Lifts an option into a synchronous flow with the supplied error.
- [`Flow.Flow.fromValueOption`](./construction/m-flow-flow-fromvalueoption.md): Lifts a value option into a synchronous flow with the supplied error.
- [`Flow.Flow.fromAsync`](./construction/m-flow-flow-fromasync.md): Creates a flow from a raw async operation.
- [`Flow.Flow.attemptAsync`](./construction/m-flow-flow-attemptasync.md): Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.fromTask`](./construction/m-flow-flow-fromtask.md): Creates a flow from a raw task operation.
- [`Flow.Flow.attemptTask`](./construction/m-flow-flow-attempttask.md): Creates a flow from a task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.fromValueTask`](./construction/m-flow-flow-fromvaluetask.md): Creates a flow from a raw value task operation.
- [`Flow.Flow.attemptValueTask`](./construction/m-flow-flow-attemptvaluetask.md): Creates a flow from a value task operation and treats thrown exceptions as recoverable typed errors.
- [`Flow.Flow.verify`](./composition/m-flow-flow-verify.md): Runs an environment-aware policy against an input value inside a workflow.
- [`Flow.Flow.orElseFlow`](./construction/m-flow-flow-orelseflow.md): Turns a pure validation result into a synchronous flow with environment-provided failure.
- [`Flow.Flow.env`](./environment/m-flow-flow-env.md): Reads the current environment as the successful flow value.
- [`Flow.Flow.read`](./environment/m-flow-flow-read.md): Projects one value from the current environment.
- [`Flow.Flow.map`](./composition/m-flow-flow-map.md): Transforms the successful value of a flow.
- [`Flow.Flow.bind`](./composition/m-flow-flow-bind.md): Sequences a dependent flow after a successful value.
- [`Flow.Flow.tap`](./composition/m-flow-flow-tap.md): Runs an effect on success and preserves the original value.
- [`Flow.Flow.tapError`](./composition/m-flow-flow-taperror.md): Runs a synchronous side effect on failure and preserves the original error.
- [`Flow.Flow.mapError`](./composition/m-flow-flow-maperror.md): Maps the error value of a synchronous flow.
- [`Flow.Flow.catch`](./composition/m-flow-flow-catch.md): Catches exceptions raised during execution and simple defect outcomes, then maps them to a typed error.
- [`Flow.Flow.orElseWith`](./composition/m-flow-flow-orelsewith.md): Computes a fallback flow from the typed error when the source flow fails.
- [`Flow.Flow.orElse`](./composition/m-flow-flow-orelse.md): Falls back to another flow when the source flow fails.
- [`Flow.Flow.zip`](./composition/m-flow-flow-zip.md): Runs two flows sequentially and combines their successful values into a tuple.
- [`Flow.Flow.map2`](./composition/m-flow-flow-map2.md): Combines two flows with a mapping function.
- [`Flow.Flow.map3`](./composition/m-flow-flow-map3.md): Combines three flows with a mapping function.
- [`Flow.Flow.apply`](./composition/m-flow-flow-apply.md): Applies a flow-wrapped function to a flow-wrapped value.
- [`Flow.Flow.ignore`](./composition/m-flow-flow-ignore.md): Maps the successful value of a synchronous flow to <code>unit</code>.
- [`Flow.Flow.localEnv`](./environment/m-flow-flow-localenv.md): Runs a flow against an environment derived from the outer environment.
- [`Flow.Flow.provide`](./environment/m-flow-flow-provide.md): Builds an environment with a layer, runs a downstream flow, and always closes the layer scope.
- [`Flow.Flow.delay`](./construction/m-flow-flow-delay.md): Defers flow construction until execution time.
- [`Flow.Flow.traverse`](./composition/m-flow-flow-traverse.md): Transforms a sequence of values into a flow and stops at the first failure.
- [`Flow.Flow.sequence`](./composition/m-flow-flow-sequence.md): Transforms a sequence of flows into a flow of a sequence and stops at the first failure.

## Policies

- [`Flow.Policy`](./t-flow-policy.md): Constructors and combinators for environment-aware workflow requirements.
- [`Flow.Policy.withError`](./m-flow-policy-witherror.md): Lifts a pure result-returning function and replaces any error with a fixed workflow error.
- [`Flow.Policy.context`](./m-flow-policy-context.md): Lifts an environment-aware result-returning function and maps its error into the workflow error type.
- [`Flow.Policy.pass`](./p-flow-policy-pass.md): A policy that returns the input unchanged.
- [`Flow.Policy.compose`](./m-flow-policy-compose.md): Composes two policies left to right.
- [`Flow.Policy.optional`](./m-flow-policy-optional.md): Runs a policy only when the environment predicate is true; otherwise returns the input unchanged.

## Scoped resources

- [`Flow.Flow.addFinalizer`](./resources/m-flow-flow-addfinalizer.md): Registers an asynchronous finalizer with the current runtime scope.
- [`Flow.Flow.addDisposable`](./resources/m-flow-flow-adddisposable.md): Registers a disposable resource with the current runtime scope.
- [`Flow.Flow.addAsyncDisposable`](./resources/m-flow-flow-addasyncdisposable.md): Registers an asynchronously disposable resource with the current runtime scope.
- [`Flow.Flow.acquireRelease`](./resources/m-flow-flow-acquirerelease.md): Acquires a resource and registers its release with the current runtime scope.
- [`Flow.Flow.acquireReleaseWith`](./resources/m-flow-flow-acquirereleasewith.md): Acquires a resource, uses it, and always runs the release action.

## Parallel orchestration

- [`Flow.Flow.zipPar`](./concurrency/m-flow-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`Flow.Flow.race`](./concurrency/m-flow-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.

## Scheduling

- [`Flow.Schedule.retry`](./scheduling/m-flow-schedule-retry.md): Retries a failing flow according to the supplied schedule.
- [`Flow.Schedule.repeat`](./scheduling/m-flow-schedule-repeat.md): Repeats a successful flow according to the supplied schedule.
