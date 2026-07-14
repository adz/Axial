---
title: "Flow.Hosting.addAppWith"
linkTitle: "addAppWith"
weight: 2103
---

Registers a root application with explicit Generic Host completion options.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.Hosting.addAppWith&#32;<span>options&#32;environmentFactory&#32;describeError&#32;application&#32;services</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `options` | <code><a href="t-flow-hosting-hostedappoptions.md">HostedAppOptions</a></code> |  |
| `environmentFactory` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a>&#32;->&#32;'env</span></code> |  |
| `describeError` | <code><span>'error&#32;->&#32;string</span></code> |  |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> |  |
| `services` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection">IServiceCollection</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection">IServiceCollection</a></code> |  |
