---
title: JavaScript Telemetry
description: OpenTelemetry tracing for Flow applications compiled with Fable.
---

# JavaScript Telemetry

`Axial.Telemetry.JavaScript` connects Flow tracing to the JavaScript OpenTelemetry APIs in Node or a browser.
It is separate from `Axial.Telemetry`, which provides the .NET integration.

Install the package in the Fable application that owns the JavaScript runtime boundary:

```sh
dotnet add package Axial.Telemetry.JavaScript
```

The shared tracing model and setup examples are covered in the [Telemetry guide](index.html). The
[packages and platforms matrix](/notes/packages-and-platforms.html) lists the supported targets for both
telemetry packages.
