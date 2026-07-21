---
title: JavaScript Telemetry
description: OpenTelemetry tracing for Flow applications compiled with Fable.
type: docs
---


`Axial.Flow.Telemetry.JavaScript` connects Flow tracing to the JavaScript OpenTelemetry APIs in Node or a browser.
It is separate from `Axial.Flow.Telemetry`, which provides the .NET integration.

Install the package in the Fable application that owns the JavaScript runtime boundary:

```sh
dotnet add package Axial.Flow.Telemetry.JavaScript
```

The shared tracing model and setup examples are covered in the [Telemetry guide](../). The
[packages and platforms matrix]({{< relref "/flow/packages-and-platforms/" >}}) lists the supported targets for both
telemetry packages.
