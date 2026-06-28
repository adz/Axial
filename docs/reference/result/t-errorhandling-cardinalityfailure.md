---
title: "ErrorHandling.CardinalityFailure"
linkTitle: "CardinalityFailure"
weight: 1000
---

Structured errors returned by sequence cardinality helpers.

## Signature

<div class="fsdocs-usage">
<code>type CardinalityFailure</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ExpectedSingle` | The sequence was expected to contain exactly one item. |
| `ExpectedAtMostOne` | The sequence was expected to contain at most one item. |
| `ExpectedAtLeastOne` | The sequence was expected to contain at least one item. |
| `ExpectedMoreThanOne` | The sequence was expected to contain more than one item. |
