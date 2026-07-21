---
title: "Data.ofConfiguration"
linkTitle: "ofConfiguration"
weight: 2008
---


 Builds structured data from flattened configuration keys using <code>:</code> as the path separator.


## Signature

<div class="fsdocs-usage">
<code><span>Data.ofConfiguration&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span><span>(<span>string&#32;*&#32;string</span>)</span>&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../data/t-data.md">Data</a></code> |  |

## Remarks

<p class='fsdocs-para'>
 Numeric path segments are interpreted as collection indexes, matching the common .NET configuration convention
 for arrays such as <code>contacts:0:value</code>.
 </p><p class='fsdocs-para'>
 Later pairs override earlier ones at the same path, matching .NET configuration layering: a repeated key
 keeps its last value, and a later scalar or section replaces an earlier section or scalar at that key.
 Collections come from numeric segments, never from repetition — repeated names as multi-value input is a
 wire convention that belongs to <code>ofNameValues</code>. A null value never overrides an existing section,
 because <code>IConfiguration.AsEnumerable()</code> emits every section key with a null value alongside that
 section&#39;s children.
 </p>
