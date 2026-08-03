---
title: "Schema.SchemaMessages"
linkTitle: "SchemaMessages"
weight: 1217
type: docs
---

The message keys Schema&#39;s own failures render through.

## Signature

<div class="fsdocs-usage">
<code>type SchemaMessages</code>
</div>

## Remarks

<p class='fsdocs-para'>
 Parse, boundary-supply, and structural failures are closed identities with <code>schema.*</code> keys and neutral
 English fallbacks. Constructor failures and custom errors carrying authored prose stay verbatim: Schema has no
 catalogue entry for text an application wrote.
 </p><p class='fsdocs-para'>
 Entries are bare predicates like the constraint catalogue&#39;s, so <code>SchemaErrors.messages</code> and
 <code>SchemaErrors.fullMessages</code> compose the attribute noun exactly once in either case.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Messages.fs#L21-21)
