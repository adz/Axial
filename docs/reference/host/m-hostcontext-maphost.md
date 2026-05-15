---
title: "HostContext.mapHost"
linkTitle: "mapHost"
---

<div class="fsdocs-usage">
<code><span>HostContext.mapHost&#32;<span>mapper&#32;context</span></span></code>
</div>

Maps the host half of a host context.

## Parameters

- `mapper`: <code><span>'host&#32;->&#32;'nextHost</span></code>
  A function of type <code>&#39;host -&gt; &#39;nextHost</code>.
- `context`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-hostcontext-2.html">HostContext</a>&lt;<span>'host,&#32;'appEnv</span>&gt;</span></code>
  The source context.

## Returns

A new context with the mapped host services.

