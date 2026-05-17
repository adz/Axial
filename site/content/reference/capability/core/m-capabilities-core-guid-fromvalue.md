---
title: "Capabilities.Core.Guid.fromValue"
linkTitle: "fromValue"
type: docs
---

Creates a deterministic GUID generator that always returns the supplied value.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Core.Guid.fromValue&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><a href="https://learn.microsoft.com/dotnet/api/system.guid">Guid</a></code> | The fixed GUID to return. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-capabilities-core-iguid.html">IGuid</a></code> | A mock <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-iguid.html">IGuid</a> implementation. |

