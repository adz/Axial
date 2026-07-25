---
title: "Flow.HttpClient.DSL.secret"
linkTitle: "secret"
weight: 2606
---

 Marks an interpolated URL value for diagnostic redaction.
 <example><code>GET $"https://api.example.com/users?key={secret apiKey}"</code></example>

## Signature

<div class="fsdocs-usage">
<code><span>Flow.HttpClient.DSL.secret&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-flow-httpclient-dsl-secretvalue.md">SecretValue</a></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Flow.HttpClient/Http.fs#L560-560)
