---
title: "Flow.env"
linkTitle: "env"
---

<div class="fsdocs-usage">
<code><span>env&#32;<span></span></span></code>
</div>

Reads the current environment as the successful flow value.

## Remarks


 Use this when the next step genuinely needs the whole environment value, for example when
 passing a request context to another helper. For a single dependency or configuration value,
 prefer <code>Flow.read</code>; it keeps the dependency local and makes the workflow easier to scan.
 

## Returns

A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a> whose successful value is the current environment.

