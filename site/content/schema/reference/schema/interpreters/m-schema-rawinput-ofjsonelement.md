---
title: "Schema.RawInput.ofJsonElement"
linkTitle: "ofJsonElement"
weight: 2006
type: docs
---

Builds raw input from a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a>.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.RawInput.ofJsonElement&#32;<span>element</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `element` | <code><a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Remarks

<p class='fsdocs-para'>
 This is the boundary adapter for JSON bodies parsed with <code>System.Text.Json</code>, such as ASP.NET Core request
 payloads: convert the element once, then parse it with <code>Model.parse</code> to get path-aware diagnostics or a
 trusted model. JSON null and undefined become <code>Missing</code>, numbers keep their exact boundary text, and
 booleans become <code>&quot;true&quot;</code>/<code>&quot;false&quot;</code> scalars.
 </p><p class='fsdocs-para'>
 The adapter is available on .NET 8+ targets where <code>System.Text.Json</code> ships in-box, keeping the package
 dependency-free and Fable-safe on other targets. Fable and .NET Standard callers can adapt JSON-shaped data
 through <a href="/reference/Axial/axial-schema-rawinputmodule.html#ofJsonLikeValue">RawInput.ofJsonLikeValue</a> instead.
 </p><p class='fsdocs-para'>netstandard2.1: not available.</p>
