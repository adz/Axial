---
title: "Check.fromTry"
linkTitle: "fromTry"
weight: 2201
---

Converts a .NET <code>Try*</code> tuple into a check result.

## Signature

<div class="fsdocs-usage">
<code><span>Check.fromTry&#32;<span><span>(<span>arg1,&#32;arg1</span>)</span></span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code>bool</code> |  |
| `arg1` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the value when the flag is true; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">System</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="vt">Int32</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">TryParse</span> <span class="s">&quot;42&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromTry</span> <span class="c">// Ok 42</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">System</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="vt">Int32</span><span class="pn">.</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="6" class="id">TryParse</span> <span class="s">&quot;x&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromTry</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace System</div>
<div popover class="fsdocs-tip" id="fs2">
type Int32 =
  member CompareTo: value: int -&gt; int + 1 overload
  member Equals: obj: int -&gt; bool + 1 overload
  member GetHashCode: unit -&gt; int
  member GetTypeCode: unit -&gt; TypeCode
  member ToString: unit -&gt; string + 3 overloads
  member TryFormat: utf8Destination: Span&lt;byte&gt; * bytesWritten: byref&lt;int&gt; * ?format: ReadOnlySpan&lt;char&gt; * ?provider: IFormatProvider -&gt; bool + 1 overload
  static member Abs: value: int -&gt; int
  static member BigMul: left: int * right: int -&gt; int64
  static member Clamp: value: int * min: int * max: int -&gt; int
  static member CopySign: value: int * sign: int -&gt; int
  ...<br /><em>&lt;summary&gt;Represents a 32-bit signed integer.&lt;/summary&gt;</em></div>
<div popover class="fsdocs-tip" id="fs3">System.Int32.TryParse( s: string, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(s: System.ReadOnlySpan&lt;char&gt;, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(utf8Text: System.ReadOnlySpan&lt;byte&gt;, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse( s: string, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(s: System.ReadOnlySpan&lt;char&gt;, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(utf8Text: System.ReadOnlySpan&lt;byte&gt;, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse( s: string, style: System.Globalization.NumberStyles, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(s: System.ReadOnlySpan&lt;char&gt;, style: System.Globalization.NumberStyles, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool<br />System.Int32.TryParse(utf8Text: System.ReadOnlySpan&lt;byte&gt;, style: System.Globalization.NumberStyles, provider: System.IFormatProvider, result: byref&lt;int&gt;) : bool</div>



