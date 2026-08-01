---
title: "Data.Syntax.matching"
linkTitle: "matching"
weight: 2511
type: docs
---

Checks authored expectations or raises <code>DataMatchException</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.matching&#32;<span>expectations&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expectations` | <code><span><a href="t-dataexpectation.md">DataExpectation</a>&#32;list</span></code> |  |
| `actual` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>unit</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">matching</span> <span class="pn">[</span> <span class="id">at</span> <span class="s">&quot;user.name&quot;</span> <span class="s">&quot;Ada&quot;</span><span class="pn">;</span> <span class="id">absent</span> <span class="s">&quot;error&quot;</span> <span class="pn">]</span> <span class="id">actual</span>
 <span class="c">// returns unit when both expectations hold; otherwise raises DataMatchException</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L747-747)
