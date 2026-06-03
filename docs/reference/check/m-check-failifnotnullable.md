---
title: "Check.failIfNotNullable"
linkTitle: "failIfNotNullable"
weight: 2221
---

Returns success when the nullable has no value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNotNullable&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.nullable-1">Nullable</a>&lt;'value&gt;</span></code> | The nullable value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds when the nullable has no value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">System</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">Nullable</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="vt">int</span><span class="pn">&gt;</span><span class="pn">(</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotNullable</span> <span class="c">// Ok ()</span>
 <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">System</span><span class="pn">.</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="id">Nullable</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotNullable</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace System</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />
type Nullable&lt;&#39;T (requires default constructor and value type and &#39;T :&gt; ValueType)&gt; =
  new: value: &#39;T -&gt; unit
  member Equals: other: obj -&gt; bool
  member GetHashCode: unit -&gt; int
  member GetValueOrDefault: unit -&gt; &#39;T + 1 overload
  member ToString: unit -&gt; string
  static member op_Explicit: value: Nullable&lt;&#39;T&gt; -&gt; &#39;T
  static member op_Implicit: value: &#39;T -&gt; Nullable&lt;&#39;T&gt;
  member HasValue: bool
  member Value: &#39;T<br /><em>&lt;summary&gt;Represents a value type that can be assigned &lt;see langword=&quot;null&quot; /&gt;.&lt;/summary&gt;<br />&lt;typeparam name=&quot;T&quot;&gt;The underlying value type of the &lt;see cref=&quot;T:System.Nullable`1&quot; /&gt; generic type.&lt;/typeparam&gt;</em><br /><br />--------------------<br />type Nullable =
  static member Compare&lt;&#39;T (requires default constructor and value type and &#39;T :&gt; ValueType)&gt; : n1: Nullable&lt;&#39;T&gt; * n2: Nullable&lt;&#39;T&gt; -&gt; int
  static member Equals&lt;&#39;T (requires default constructor and value type and &#39;T :&gt; ValueType)&gt; : n1: Nullable&lt;&#39;T&gt; * n2: Nullable&lt;&#39;T&gt; -&gt; bool
  static member GetUnderlyingType: nullableType: Type -&gt; Type
  static member GetValueRefOrDefaultRef&lt;&#39;T (requires default constructor and value type and &#39;T :&gt; ValueType)&gt; : nullable: inref&lt;Nullable&lt;&#39;T&gt;&gt; -&gt; inref&lt;&#39;T&gt;<br /><em>&lt;summary&gt;Supports a value type that can be assigned &lt;see langword=&quot;null&quot; /&gt;. This class cannot be inherited.&lt;/summary&gt;</em><br /><br />--------------------<br />System.Nullable ()<br />System.Nullable(value: &#39;T) : System.Nullable&lt;&#39;T&gt;</div>
<div popover class="fsdocs-tip" id="fs3">Multiple items<br />val int: value: &#39;T -&gt; int (requires member op_Explicit)<br /><br />--------------------<br />type int = int32<br /><br />--------------------<br />type int&lt;&#39;Measure&gt; =
  int</div>
