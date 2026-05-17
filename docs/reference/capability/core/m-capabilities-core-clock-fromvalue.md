---
title: "Capabilities.Core.Clock.fromValue"
linkTitle: "fromValue"
---

Creates a deterministic clock that always returns the supplied instant.

## Signature

<div class="fsdocs-usage">
<code><span>Capabilities.Core.Clock.fromValue&#32;<span>utcNow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `utcNow` | <code><a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset">DateTimeOffset</a></code> | The fixed timestamp to return from the clock. |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-capabilities-core-iclock.html">IClock</a></code> | A mock <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-iclock.html">IClock</a> implementation. |

