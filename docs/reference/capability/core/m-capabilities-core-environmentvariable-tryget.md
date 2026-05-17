---
title: "Capabilities.Core.EnvironmentVariable.tryGet"
linkTitle: "tryGet"
---

Reads a raw string environment variable without wrapping it in a result.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Core.EnvironmentVariable.tryGet&#32;<span>name</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> | The name of the environment variable. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'e,&#32;<span>string&#32;option</span></span>&gt;</span></code> | A flow that produces the variable value if it exists. |

