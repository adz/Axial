---
title: "Flow.Http.DSL.secret"
linkTitle: "secret"
weight: 2606
---

 Marks an interpolated URL value for diagnostic redaction.
 <example><code>GET $"https://api.example.com/users?key={secret apiKey}"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Http.DSL.secret&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="/reference/Axial/axial-flow-http-dsl-secretvalue.html">SecretValue</a></code> |  |
