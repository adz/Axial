---
weight: 15
title: Flow Tutorials
description: Service, runtime, layer, and environment tutorials for Flow.
---

# Flow Tutorials

These tutorials cover Flow service boundaries, environment records, runtime operations, and layers.

## Which Tutorial Should I Use?

| Tutorial | Focus | Best For |
| :--- | :--- | :--- |
| **[Explicit Dependencies First](./explicit-dependencies/)** | Plain function arguments | The first step: prove your interfaces and workflow shape before introducing an environment. |
| **[App Record](./app-record/)** | Concrete environment records | Features that now need several dependencies but still want direct, low-ceremony field access. |
| **[Runtime Operations](./runtime-operations/)** | Timeout, retry, cancellation, annotations | Operational boundaries around a workflow execution. |
| **[Using Existing Services](./existing-services/)** | Standard Axial service packages | Apps that want built-in clock, logging, or environment-variable services in an explicit env. |
| **[Creating Reusable Services](./custom-services/)** | Named service contracts | Shared helpers that should depend on `IHas<'service>` instead of one record field name. |
| **[Layers](./layers/)** | Provisioned environments and resource ownership | Apps that need startup construction, cleanup, provisioning failure, or scope-owned resources. |

If you are new to Flow, start with **Explicit Dependencies First**, then move to **App Record**.
