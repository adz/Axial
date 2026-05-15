---
title: "HostContext.mapAppEnv"
linkTitle: "mapAppEnv"
---

<div class="fsdocs-usage">
<code><span>HostContext.mapAppEnv&#32;<span>mapper&#32;context</span></span></code>
</div>

Maps the application environment half of a host context.

## Parameters

- `mapper`: <code><span>'appEnv&#32;->&#32;'nextAppEnv</span></code>
  A function of type <code>&#39;appEnv -&gt; &#39;nextAppEnv</code>.
- `context`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-hostcontext-2.html">HostContext</a>&lt;<span>'host,&#32;'appEnv</span>&gt;</span></code>
  The source context.

## Returns

A new context with the mapped app environment.

