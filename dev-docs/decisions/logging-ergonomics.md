# Logging Ergonomics

Status: decided.
Recorded: 2026-05-03.

## Extracted From

- `dev-docs/PLAN.md`:
  - the open question about core logging versus `ILogger` adapters
- `dev-docs/TASKS.md`:
  - task 23

## Decision

Keep the core logging abstraction generic.

- The core shape stays centered on the environment-projected writer and `LogEntry`.
- `ILogger` should remain an adapter at the application or host boundary.
- The library surface should keep the generic `log` / `logWith` helpers instead of hard-wiring Microsoft.Extensions.Logging into the core contracts.

## Why

- A generic core keeps `FsFlow` dependency-light and usable outside `Microsoft.Extensions.Logging`.
- It matches the explicit service-capability direction and keeps logging separate from host-specific adapters.
- Application code can still adapt existing `ILogger` instances without forcing that dependency on every consumer.
- Ergonomics belong in the integration layer, not in the core type identity.

## Consequences

- Core documentation should describe logging as a generic service capability.
- .NET integration examples may continue to use `ILogger` as the adapter shape.
- `ILogger` stays a bridge rather than becoming the core logging abstraction.
