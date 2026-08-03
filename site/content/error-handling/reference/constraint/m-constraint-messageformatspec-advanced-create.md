---
title: "Constraint.MessageFormatSpec.Advanced.create"
linkTitle: "create"
weight: 2815
type: docs
---

Builds a specification, raising when the plural operand names no argument.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageFormatSpec.Advanced.create&#32;<span>fallback&#32;pluralArgument&#32;descriptor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallback` | <code>string</code> |  |
| `pluralArgument` | <code><span>string&#32;option</span></code> |  |
| `descriptor` | <code><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-messageformatspec.md">MessageFormatSpec</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">MessageFormatSpec</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">create</span> <span class="s">&quot;must be at least {expected}&quot;</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">None</span> <span class="id">descriptor</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.None: Option&lt;&#39;T&gt;</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L223-223)
