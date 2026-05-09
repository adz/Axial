---
title: CAPS Core
type: docs
weight: 131
---

# CAPS Core

`FsFlow.Caps.Core` is the smallest shared capability package in the FsFlow CAPS story. It keeps the surface synchronous and explicit:

- `Clock` for time
- `Random` for deterministic or live random values
- `Guid` for GUID generation
- `EnvironmentVariables` and `EnvironmentVariable` for typed environment lookups

The package is designed so callers can use the live providers in production and fixed providers in tests without changing the call shape.

## Reference

- [Core API](./core.md): the source-documented module and type surface for the package.
