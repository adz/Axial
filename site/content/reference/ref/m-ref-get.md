---
title: "Ref.get"
linkTitle: "get"
weight: 2101
type: docs
---

Reads the current value of the reference.

## Signature

<div class="fsdocs-usage">
<code><span>Ref.get&#32;<span>reference</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `reference` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-ref-1.html">Ref</a>&lt;'T&gt;</span></code> | The <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-ref-1.html">Ref</a> to read from. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'none,&#32;'T</span>&gt;</span></code> | A flow that returns the current value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Ref</span><span class="pn">.</span><span class="id">get</span> <span class="id">myRef</span>
</code></pre>
