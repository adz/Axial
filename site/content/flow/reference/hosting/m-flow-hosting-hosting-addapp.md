---
title: "Flow.Hosting.addApp"
linkTitle: "addApp"
weight: 2102
type: docs
---

Registers a root application that owns the Generic Host lifetime.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.Hosting.addApp&#32;<span>environmentFactory&#32;describeError&#32;application&#32;services</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environmentFactory` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a>&#32;->&#32;'env</span></code> |  |
| `describeError` | <code><span>'error&#32;->&#32;string</span></code> |  |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> |  |
| `services` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection">IServiceCollection</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection">IServiceCollection</a></code> |  |
