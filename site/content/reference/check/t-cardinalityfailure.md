---
title: "CardinalityFailure"
linkTitle: "CardinalityFailure"
weight: 1101
type: docs
---

Structured errors returned by sequence cardinality checks.

## Signature

<div class="fsdocs-usage">
<code>type CardinalityFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ExpectedExactlyOne` | The sequence was expected to contain exactly one item. |
| `ExpectedNotExactlyOne` | The sequence was expected to contain zero items or more than one item. |
| `ExpectedAtMostOne` | The sequence was expected to contain at most one item. |
| `ExpectedMoreThanOne` | The sequence was expected to contain more than one item. |
