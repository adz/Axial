---
title: "Scope.AddChild"
linkTitle: "AddChild"
weight: 2103
---

Creates a child scope whose cleanup is owned by this scope.

## Signature

<div class="fsdocs-usage">
<code><span>this.AddChild</span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-scope.html">Scope</a></code> | A child scope that is closed when this scope closes. |

## Remarks


 Child scopes make parallel acquisition deterministic: each branch can register its own
 finalizers, while the parent decides the fixed order in which branch scopes are closed.
