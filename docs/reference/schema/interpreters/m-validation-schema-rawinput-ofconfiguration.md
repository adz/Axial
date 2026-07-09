---
title: "Validation.Schema.RawInput.ofConfiguration"
linkTitle: "ofConfiguration"
weight: 2008
---


 Builds raw input from flattened configuration keys using <code>:</code> as the path separator.


## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.RawInput.ofConfiguration&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span><span>(<span>string&#32;*&#32;string</span>)</span>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-validation-schema-rawinput.md">RawInput</a></code> |  |

## Remarks

<p class='fsdocs-para'>
 Numeric path segments are interpreted as collection indexes, matching the common .NET configuration convention
 for arrays such as <code>contacts:0:value</code>.
 </p>
