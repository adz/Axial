---
title: "Refined.RefinementError"
linkTitle: "RefinementError"
weight: 1001
---

Structural failures returned by built-in refinement constructors and the <code>refine { }</code> builder.

## Signature

<div class="fsdocs-usage">
<code>type RefinementError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `ParseFailed` | A primitive parse operation failed before refinement. |
| `CheckFailed` | An executable <a href="../check/t-errorhandling-check.md">Check</a> program run against the target refined type
 failed. Carries the same structured <a href="../result/t-errorhandling-checkfailure.md">CheckFailure</a> values the check program
 produced, so callers never need to reinterpret or re-describe them. |
| `InvalidStructure` | The value had an invalid structure for the target refined type that a single-value
 <a href="../check/t-errorhandling-check.md">Check</a> program cannot express, such as a cross-field ordering
 invariant. |
