# FsFlow Tasks

This file is the active development queue. Keep completed work out of this file.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep historical research in `dev-docs/deprecated/caps-research/` and settled decisions in `dev-docs/decisions/`.

## Priority Order

Work this queue from top to bottom.

1. [x] Replace `Flow.service` and `Flow.inject` with `Service<'service>.get()` and `Service<'service>.resolve()`.
   - Add the new accessor type.
   - Migrate internal usage, tests, examples, and reference docs.
   - Remove the old accessors from the target public surface.

2. [x] Shrink ambient runtime to executor mechanics only.
   - Keep cancellation, scope, annotations, interruption, and scheduling helpers.
   - Remove ambient `IClock`, `ILog`, `IRandom`, `IGuid`, and `IEnvironmentVariables` access.
   - Remove `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`.

3. [x] Make `Scope` and `Layer` first-class public architecture.
   - Public `Scope` needs async-capable finalizers and deterministic teardown semantics.
   - Public `Layer` needs a minimal provisioning surface plus `Flow.provide`.
   - Replace the current flow-based `provideLayer` with the new layer-based API.

4. [x] Delete registry-backed runtime infrastructure.
   - Remove `RuntimeRegistry.fs`.
   - Remove `RuntimeAdapter.fs`.
   - Remove registry-based assumptions from `RuntimeLayer.fs`.
   - Keep no internal "future second DI system" alive after the redesign lands.

5. [x] Rebuild former ambient core services as explicit services.
   - `Clock`, `Log`, `Random`, `Guid`, and `EnvironmentVariables` should become wrappers over explicit services.
   - Provide a standard `BaseRuntime` bundle and layer story for live/test environments.
   - `FsFlow.Services.Core` owns the base-runtime bundle and live/provider-backed layer helpers.

6. [x] Align first-party service packages to the same service-plus-layer model.
   - Console
   - FileSystem
   - Http
   - Process
   - future Network remains a new package.
   - telemetry remains a runtime instrumentation package until its service contract is designed.

7. [ ] Finish the public docs rewrite around explicit services and layers.
   - Replace the old dependency-model guides with a new services-and-layers narrative.
   - Add guides for service contracts, provider boundaries, layers, scopes/resources, and base runtime construction.
   - Update `llms.txt` and `docs/AGENT.md`.

8. [ ] Design future service packages after the core v1 surface is stable.
   - Network
   - telemetry service contracts, if needed beyond runtime instrumentation

## Acceptance Checks

The current architecture is coherent when the following are true:

- public docs describe services as explicit and the runtime as executor-only
- user-facing workflow signatures show real service requirements in `'env`
- app/domain dependency examples start with records and `Flow.read`
- reusable service examples use `Service<'service>.get()`
- host-edge examples use `Service<'service>.resolve()` or provider-backed layers
- `Layer` is the documented provisioning mechanism
- registry-backed runtime is gone from both code and docs
- generated reference docs match source comments
