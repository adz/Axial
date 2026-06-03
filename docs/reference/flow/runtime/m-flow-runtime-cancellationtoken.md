---
title: "Flow.Runtime.cancellationToken"
linkTitle: "cancellationToken"
weight: 2000
---

Reads the current runtime cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.cancellationToken&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></span>&gt;</span></code> | A flow that succeeds with the token supplied to <a href="https://learn.microsoft.com/dotnet/api/runfull">runFull</a> or <a href="https://learn.microsoft.com/dotnet/api/runwithtoken">runWithToken</a>. |
