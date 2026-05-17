---
title: "Capabilities.Core.EnvironmentVariable.getGuid"
linkTitle: "getGuid"
type: docs
---

Reads a GUID environment variable from the ambient runtime.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Core.EnvironmentVariable.getGuid&#32;<span>name</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `name` | <code>string</code> | The name of the environment variable. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;<a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-capabilities-core-environmentvariableerror.html">EnvironmentVariableError</a>,&#32;<a href="https://learn.microsoft.com/dotnet/api/system.guid">Guid</a></span>&gt;</span></code> | A flow that produces the parsed GUID or fails if missing or invalid. |

