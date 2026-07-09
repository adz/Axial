---
title: "Schema.Value.optionOf"
linkTitle: "optionOf"
weight: 2107
type: docs
---

Describes an optional value so <code>&#39;field option</code> models are schema-describable.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Value.optionOf&#32;<span>payload</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `payload` | <code><span><a href="t-schema-valueschema.md">ValueSchema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-valueschema.md">ValueSchema</a>&lt;<span>'value&#32;option</span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 Optional value schemas make absence a legal parse result rather than a diagnostic: input parsing maps missing
 or null raw input to <code>None</code> and parses present input through <span class="fsdocs-param-name">payload</span> into <code>Some</code>,
 with the payload schema&#39;s constraints running on the payload. Codecs decode an absent or <code>null</code> JSON field
 to <code>None</code> and omit <code>None</code> fields when encoding, and JSON Schema generation leaves optional fields out
 of the object&#39;s <code>required</code> list.
 </p><p class='fsdocs-para'>
 Optionality is a single boundary layer, not a nestable wrapper: <code>optionOf (optionOf ...)</code> is rejected
 because absent input could not distinguish <code>None</code> from <code>Some None</code>. Combining <code>optionOf</code> with
 the <code>required</code> constraint is contradictory and is rejected here when the payload carries it, by
 <code>Value.withConstraint</code> when attached to the optional schema itself, and by <code>Schema.build</code> when
 attached at the field level.
 </p>
