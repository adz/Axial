---
title: "Data.ofJsonElement"
linkTitle: "ofJsonElement"
weight: 2006
type: docs
---

Builds structured data from a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a>.

## Signature

<div class="fsdocs-usage">
<code><span>Data.ofJsonElement&#32;<span>element</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `element` | <code><a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Remarks

<p class='fsdocs-para'>
 This is the boundary adapter for JSON bodies parsed with <code>System.Text.Json</code>, such as ASP.NET Core request
 payloads: convert the element once, then parse it with <code>Schema.parse</code> to get path-aware diagnostics or a
 trusted model. JSON value kinds remain distinct, and number tokens are carried without narrowing them to one
 CLR numeric type. Other JSON syntax, such as whitespace and source locations, is not represented.
 </p><p class='fsdocs-para'>
 The adapter is available on .NET 8+ targets where <code>System.Text.Json</code> ships in-box, keeping the package
 dependency-free and Fable-safe on other targets.
 </p><p class='fsdocs-para'>netstandard2.1: not available.</p>
