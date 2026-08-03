---
title: "Constraint.customLocalized"
linkTitle: "customLocalized"
weight: 2305
---


 Runs an arbitrary predicate, reporting the supplied prose and the author&#39;s own catalogue key when it fails.


## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Constraint.customLocalized&#32;<span>key&#32;description&#32;predicate</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `key` | <code>string</code> |  |
| `description` | <code>string</code> |  |
| `predicate` | <code><span>'value&#32;->&#32;bool</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraint.md">Constraint</a>&lt;'value&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'>
 Opaque exactly as <code>custom</code> is — the key names a message, not a rule, and claims nothing inspectable.
 What it buys is translation: a failure from plain <code>custom</code> projects as verbatim prose, which no
 resource system can look up, so an application with localization and one custom rule would otherwise have
 one permanently untranslatable message.
 </p><p class='fsdocs-para'>
 The prose is still required, and remains the default English rendering. Axial supplies no key of its own
 here; only an author who has a catalogue can name an entry in it.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Constraint</span><span class="pn">.</span><span class="id">customLocalized</span>
     <span class="s">&quot;books.isbn.invalid&quot;</span>
     <span class="s">&quot;must be a valid ISBN&quot;</span>
     <span class="id">isValidIsbn</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L253-253)
