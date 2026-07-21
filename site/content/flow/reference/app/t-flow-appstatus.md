---
title: "Flow.AppStatus"
linkTitle: "AppStatus"
weight: 1000
type: docs
---

Describes the lifecycle state of a running application.

## Signature

<div class="fsdocs-usage">
<code>type AppStatus</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Running` | The root workflow is running. |
| `Stopping` | Stop has been requested and the root workflow is finishing cleanup. |
| `Completed` | The root workflow and all of its scope finalizers have completed. |
