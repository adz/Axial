---
title: "HostContext.create"
linkTitle: "create"
type: docs
---

<div class="fsdocs-usage">
<code><span>HostContext.create&#32;<span>host&#32;appEnv&#32;cancellationToken</span></span></code>
</div>

Creates a host context from the supplied host services, app environment, and cancellation token.

## Parameters

- `host`: <code>'host</code>
  The host services of type <code>&#39;host</code>.
- `appEnv`: <code>'appEnv</code>
  The application environment of type <code>&#39;appEnv</code>.
- `cancellationToken`: <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code>
  The <a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>.

## Returns

A new <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-hostcontext-2.html">HostContext</a>.

