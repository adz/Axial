---
title: "CardinalityFailure"
linkTitle: "CardinalityFailure"
weight: 1100
---

Structured errors returned by sequence cardinality helpers that preserve useful diagnostics.

## Signature

<div class="fsdocs-usage">
<code>type CardinalityFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ExpectedExactlyOne` | The sequence was expected to contain exactly one item. |
| `ExpectedAtMostOne` | The sequence was expected to contain at most one item. |
