---
title: "Flow.Runtime.tryGetEnvironmentVariable"
linkTitle: "tryGetEnvironmentVariable"
type: docs
---

Reads an environment variable from the ambient runtime environment provider.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.tryGetEnvironmentVariable&#32;<span>name</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> | The name of the environment variable. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;<span>string&#32;option</span></span>&gt;</span></code> | A flow that returns the variable value if it exists, or None. |

