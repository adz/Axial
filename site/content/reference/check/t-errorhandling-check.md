---
title: "ErrorHandling.Check"
linkTitle: "Check"
weight: 1000
type: docs
---


 Typed value-check programs for local structural facts.


## Signature

<div class="fsdocs-usage">
<code>type Check</code>
</div>

## Remarks


 Top-level <code>Check.*</code> helpers return structured results, not booleans. Direct modules such as
 <code>Check.String</code>, <code>Check.Number</code>, <code>Check.Seq</code>, <code>Check.Option</code>, <code>Check.ValueOption</code>,
 <code>Check.Nullable</code>, and <code>Check.Result</code> contain the type-specific implementations. Top-level helpers such
 as <code>lengthBetween</code>, <code>between</code>, and <code>countBetween</code> are aliases for common single-target checks, while
 <code>present</code>, <code>empty</code>, and <code>notEmpty</code> are the small type-directed facade.
