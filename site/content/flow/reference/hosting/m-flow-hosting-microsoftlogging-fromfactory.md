---
title: "Flow.Hosting.MicrosoftLogging.fromFactory"
linkTitle: "fromFactory"
weight: 2201
type: docs
---

Creates an Axial logger with an explicit Microsoft logging category.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Hosting.MicrosoftLogging.fromFactory&#32;<span>categoryName&#32;loggerFactory</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `categoryName` | <code>string</code> |  |
| `loggerFactory` | <code><a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.iloggerfactory">ILoggerFactory</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../service/core/t-flow-platformservice-ilog.md">ILog</a></code> |  |
