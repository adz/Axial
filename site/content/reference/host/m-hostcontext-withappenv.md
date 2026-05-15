---
title: "HostContext.withAppEnv"
linkTitle: "withAppEnv"
type: docs
---

<div class="fsdocs-usage">
<code><span>HostContext.withAppEnv&#32;<span>appEnv&#32;context</span></span></code>
</div>

Replaces the application environment half of a host context.

## Parameters

- `appEnv`: <code>'nextAppEnv</code>
  The new application environment.
- `context`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-hostcontext-2.html">HostContext</a>&lt;<span>'host,&#32;'appEnv</span>&gt;</span></code>
  The source context.

## Returns

A new context with the replaced app environment.

