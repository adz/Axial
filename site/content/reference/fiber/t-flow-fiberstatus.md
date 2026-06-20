---
title: "Flow.FiberStatus"
linkTitle: "FiberStatus"
weight: 1002
type: docs
---

Describes the current lifecycle state of a fiber.

## Signature

<div class="fsdocs-usage">
<code>type FiberStatus</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Running` | The fiber is currently running. |
| `Succeeded` | The fiber completed with a successful value. |
| `Failed` | The fiber completed with a typed failure or defect. |
| `Interrupted` | The fiber completed with an interruption cause. |
