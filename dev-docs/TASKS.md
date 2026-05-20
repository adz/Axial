# FsFlow Tasks

This file is the active development queue. Keep completed work out of this file.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep historical research in `dev-docs/caps-research/` and settled decisions in `dev-docs/decisions/`.

## Priority Order

Work this queue from top to bottom.

1. [ ] Reconcile dormant registry/scope/layer internals with the active ambient-runtime model.
   - Confirm whether `RuntimeRegistry.fs`, `RuntimeScope.fs`, `RuntimeAdapter.fs`, and `RuntimeLayer.fs` should stay
     compiled into core while they are not used by normal workflow execution.
   - If they stay, document them in dev-docs as deferred internal infrastructure only.
   - If they move, place them behind a clearer experimental or future-facing boundary.
   - Do not claim registry-backed runtime behavior in public docs until it is actually wired into `Flow.run` or runtime
     provisioning.

2. [ ] Keep public docs centered on ambient runtime plus explicit app environment.
   - `docs/managing-dependencies/runtime-vs-environment.md` is the main architecture guide.
   - Runtime services must stay out of end-user `'env` signatures in examples unless they are deliberately modeled as
     app/domain dependencies.
   - Teach records plus `Flow.read` first.
   - Teach `IHas<'T>` plus `Flow.service` as an opt-in strict app contract.
   - Teach `Flow.inject` as host-edge interop.
   - Avoid `RuntimeContext<'runtime, 'env>` and registry-backed runtime claims in user-facing docs.

3. [ ] Audit capability examples for old runtime-as-env patterns.
   - Review `examples/FsFlow.Capabilities.Core.Examples/CoreCapabilitiesExample.fs`.
   - Review `examples/FsFlow.Examples/CapabilityExamples.fs`.
   - Keep examples that demonstrate `IHas<'T>` for app dependencies.
   - Avoid suggesting that Core runtime services such as `IClock`, `IRandom`, or `IGuid` should normally be carried by
     app environments.

4. [ ] Decide the long-term `Flow.inject` failure surface.
   - Current implementation throws when an `IServiceProvider` registration is missing, which becomes `Cause.Die`.
   - Decide whether to keep that behavior as a configuration defect or introduce an explicit typed helper for callers
     who want `MissingCapability` in the error channel.
   - Update source comments and reference docs after the decision.

5. [ ] Clarify capability package maturity.
   - Core is active and used by docs/tests.
   - Console, FileSystem, Http, and Process exist but remain experimental capability packages.
   - Decide whether each package needs its own ambient override helper before being promoted.
   - Keep package release status aligned with `dev-docs/MAINTENANCE.md`.

6. [ ] Revisit richer scope/layer support only after the current docs are stable.
   - Scope/layer work may need the registry.
   - Candidate features:
     - tagged runtime services
     - resource-producing layers
     - deterministic teardown across composed layers
     - host provisioning into runtime services
   - This is not required for the current ambient runtime capability story.

## Acceptance Checks

The current architecture is coherent when the following are true:

- public docs describe runtime services as ambient and locally overridable
- user-facing workflow signatures keep operational services out of `'env`
- app/domain dependency examples start with records and `Flow.read`
- `IHas<'T>` is documented as useful but optional
- `Flow.inject` is documented as host-edge interop
- dev-docs no longer present registry-backed runtime as the completed foundation
- generated reference docs match source comments
