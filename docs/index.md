---
title: Axial
---

<div class="docs-home-container axial-landing">
<div class="docs-home-hero">
<div class="docs-home-hero-visual">
<img class="hero-lockup hero-lockup--light" data-theme-variant="light" src="content/img/hero-lockup-light.png" alt="Axial" width="1560" height="600" />
<img class="hero-lockup hero-lockup--dark" data-theme-variant="dark" src="content/img/hero-lockup-dark.png" alt="Axial" width="1560" height="600" />
</div>
<div class="docs-home-copy">
<span class="eyebrow">Typed asynchronous workflows for F#</span>
<h1>Dependencies in the type. Supplied at the edge.</h1>
<div class="docs-home-example">
<span class="docs-home-example-label">A complete checkout workflow</span>
<pre><code class="language-fsharp">type CheckoutEnv =
    { FindTotal: int -&gt; Task&lt;Result&lt;decimal, CheckoutError&gt;&gt;
      ApplyDiscount: decimal -&gt; Result&lt;decimal, CheckoutError&gt;
      Charge: decimal -&gt; Async&lt;Result&lt;Payment, CheckoutError&gt;&gt; }
&#8203;
let checkout orderId : Flow&lt;CheckoutEnv, CheckoutError, Receipt&gt; =
    flow {
        let! env = Flow.env
&#8203;
        let! subtotal = env.FindTotal orderId       // Task&lt;Result&lt;_, _&gt;&gt;
        let! total = env.ApplyDiscount subtotal     // Result&lt;_, _&gt;
        let! payment = env.Charge total             // Async&lt;Result&lt;_, _&gt;&gt;
&#8203;
        return
            { OrderId = orderId
              Total = total
              PaymentId = payment.Id }
    }
&#8203;
let exit = checkout 42 |&gt; Flow.run live</code></pre>
</div>
<div class="docs-home-benefits" aria-label="Flow benefits">
<span><strong aria-hidden="true">✓</strong> Typed expected failures</span>
<span><strong aria-hidden="true">✓</strong> Built-in cancellation</span>
<span><strong aria-hidden="true">✓</strong> Plain-record dependencies</span>
</div>
<div class="lede">
<p>Cancellation, dependencies, and expected failures stop being extra arguments the caller has to thread through. They become part of the type.</p>
</div>
<p>Pass a workflow a plain record and get typed failures, cancellation, resource scopes, and structured concurrency. There is no container and no registration step. Axial adds retries, streams, STM, operational services, hosting, and telemetry over the same workflow model.</p>
<p><a class="btn btn-primary" href="getting-started/index.html">Run your first workflow</a></p>
<p class="docs-home-note">Axial is pre-1.0. Its API can change before the first stable release.</p>
</div>
</div>
<div class="docs-home-meta">
<a class="docs-chip" href="getting-started/index.html">Documentation</a>
<a class="docs-chip" href="https://github.com/adz/Axial">GitHub</a>
<a class="docs-chip" href="https://github.com/adz/Reified">Reified</a>
</div>
</div>
