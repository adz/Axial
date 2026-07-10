---
title: "Schema.RawInput.ofCliArgs"
linkTitle: "ofCliArgs"
weight: 2004
---


 Builds raw input from command-line arguments.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.RawInput.ofCliArgs&#32;<span>args</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `args` | <code><span>string&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-schema-rawinput.md">RawInput</a></code> |  |

## Remarks

<p class='fsdocs-para'>
 Supports <code>--name value</code>, <code>--name=value</code>, <code>-n value</code>, boolean flags, <code>--no-name</code>, and repeated
 options. Positional arguments are stored under the <code>_</code> field as a collection.
 </p>
