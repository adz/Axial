---
title: "ErrorHandling.CheckLengthExpectation"
linkTitle: "CheckLengthExpectation"
weight: 1002
---

Describes the length requirement that a value check expected a string-like value to satisfy.

## Signature

<div class="fsdocs-usage">
<code>type CheckLengthExpectation</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `MinimumLength` | The value was expected to have at least the supplied length. |
| `MaximumLength` | The value was expected to have at most the supplied length. |
| `ExactLength` | The value was expected to have exactly the supplied length. |
| `LengthBetween` | The value was expected to have a length inside the inclusive bounds. |
