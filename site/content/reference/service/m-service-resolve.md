---
title: "Service.resolve"
linkTitle: "resolve"
weight: 2101
type: docs
---

Resolves a service dynamically from an <a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a> environment.

## Signature

<div class="fsdocs-usage">
<code><span>Service.resolve&#32;<span>()</span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'service</span>&gt;</span></code> | A flow that succeeds with the requested service instance. |

## Remarks


 Missing registrations are treated as configuration defects and therefore fail through
 <code>Cause.Die</code> rather than the typed error channel.
