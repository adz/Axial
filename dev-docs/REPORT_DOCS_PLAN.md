# Superseded Documentation and Implementation Plan

This report is retained as historical context only.

It has been superseded by `dev-docs/PLAN.md`, which records the active model:

- app/domain dependencies live in explicit `'env`
- runtime/system services are ambient and do not appear in end-user workflow signatures
- `Flow.read`, `Flow.service`, and `Flow.inject` are the active app dependency accessors
- `Flow.Runtime` and `FsFlow.Capabilities.Core` are the active runtime service accessors
- `Registry`, `Scope`, `RuntimeAdapter`, and `RuntimeLayer` are internal deferred infrastructure, not the current
  runtime storage engine

The older version of this report proposed moving the runtime to a registry-backed model. That is not the current
implementation and should not be treated as live direction.
