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
<h1>One signature instead of four arguments.</h1>
<div class="docs-home-signature">
<div class="docs-home-signature-pane">
<span class="docs-home-signature-label">Without Axial</span>
<pre><code class="language-fsharp">val loadUser:
    cancellationToken: CancellationToken -&gt;
    services: AppServices -&gt;
    userId: UserId -&gt;
        Task&lt;Result&lt;User, LoadUserError&gt;&gt;</code></pre>
</div>
<div class="docs-home-signature-pane">
<span class="docs-home-signature-label">With Axial</span>
<pre><code class="language-fsharp">val loadUser:
    UserId -&gt; Flow&lt;AppServices, LoadUserError, User&gt;</code></pre>
</div>
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
