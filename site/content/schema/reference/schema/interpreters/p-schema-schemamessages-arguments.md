---
title: "Schema.SchemaMessages.arguments"
linkTitle: "arguments"
weight: 2219
type: docs
---

The argument names each Schema entry interpolates.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.SchemaMessages.arguments&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<span>string&#32;list</span></span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">SchemaMessages</span><span class="pn">.</span><span class="id">arguments</span><span class="pn">.</span><span class="pn">[</span><span class="s">&quot;schema.invalidFormat&quot;</span><span class="pn">]</span> <span class="c">// [ &quot;expected&quot; ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Messages.fs#L62-62)
