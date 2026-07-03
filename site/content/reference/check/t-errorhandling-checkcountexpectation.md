---
title: "ErrorHandling.CheckCountExpectation"
linkTitle: "CheckCountExpectation"
weight: 1004
type: docs
---

Describes the count requirement that a value check expected a collection to satisfy.

## Signature

<div class="fsdocs-usage">
<code>type CheckCountExpectation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MinimumCount` | The collection was expected to contain at least the supplied count. |
| `MaximumCount` | The collection was expected to contain at most the supplied count. |
| `ExactCount` | The collection was expected to contain exactly the supplied count. |
| `CountBetween` | The collection was expected to contain a count inside the inclusive bounds. |
