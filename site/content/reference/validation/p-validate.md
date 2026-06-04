---
title: "validate"
linkTitle: "validate { }"
weight: 2000
type: docs
---


 The accumulating <code>validate { }</code> computation expression.


## Signature

<div class="fsdocs-usage">
<code><span>validate&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><a href="builders-validate.md">ValidateBuilder</a></code> | A <a href="builders-validate.md">ValidateBuilder</a> instance. |

## Remarks

<p class='fsdocs-para'>
 Use this builder when you want to collect all validation failures instead of stopping
 at the first one.
 </p><p class='fsdocs-para'>
 Use <code>and!</code> when sibling validations should accumulate into the same diagnostics graph.
 Plain <code>let!</code> and <code>do!</code> are sequential: if the left side fails, the later step is
 not evaluated.
 </p><p class='fsdocs-para'><code>Check&lt;&#39;value&gt;</code> covers both value-preserving checks and gate checks.
 Use <code>Check.orError</code> to attach an application error, and <code>Guard.Of</code> /
 <code>Guard.MapError</code> when you want the same error-bound source shape to participate
 directly in validation.
 </p><p class='fsdocs-para'>
 When nested API response fields need to keep their place in the diagnostics graph, use
 the scoped helpers <code>validate.key</code>, <code>validate.index</code>, and <code>validate.name</code>
 inside the computation expression. If you already have a <code>Validation</code> value, use
 <code>Validation.key</code>, <code>Validation.index</code>, or <code>Validation.name</code> to prefix it
 after the fact.
 </p><p class='fsdocs-para'>
 It is intended for forms, configuration checks, and other input-heavy boundaries where
 the user benefits from seeing every problem at once.
 </p>

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">validatedUser</span> <span class="o">=</span>
     <span class="id">validate</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">name</span> <span class="o">=</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="id">input</span><span class="pn">.</span><span class="id">Name</span>
         <span class="k">let!</span> <span class="id">age</span> <span class="o">=</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="pn">(</span><span class="id">input</span><span class="pn">.</span><span class="id">Age</span> <span class="pn">&gt;</span> <span class="n">0</span><span class="pn">)</span> <span class="s">&quot;Age must be positive&quot;</span>
         <span class="k">return</span> <span class="pn">{</span> <span class="id">Name</span> <span class="o">=</span> <span class="id">name</span><span class="pn">;</span> <span class="id">Age</span> <span class="o">=</span> <span class="id">age</span> <span class="pn">}</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val validatedUser: obj</div>

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">validatedCustomer</span> <span class="o">=</span>
     <span class="id">validate</span><span class="pn">.</span><span class="id">key</span> <span class="s">&quot;customer&quot;</span> <span class="pn">{</span>
         <span class="k">let!</span> <span class="id">name</span> <span class="o">=</span>
             <span class="id">validate</span><span class="pn">.</span><span class="id">name</span> <span class="s">&quot;Name&quot;</span> <span class="pn">{</span>
                 <span class="k">return!</span> <span class="id">input</span><span class="pn">.</span><span class="id">Name</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">orError</span> <span class="s">&quot;Name required&quot;</span>
             <span class="pn">}</span>

         <span class="k">return</span> <span class="id">name</span>
     <span class="pn">}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val validatedCustomer: obj</div>
