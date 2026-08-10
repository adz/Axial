---
title: Axial
---

<div class="docs-home-container axial-landing">
<div class="docs-home-hero">
<div class="docs-home-copy">
<span class="eyebrow">Typed asynchronous workflows for F#</span>
<h1>Dependencies in the type. Supplied at the edge.</h1>
<div class="docs-home-example" aria-label="Axial dependency and runtime model">
<span class="docs-home-example-label">One workflow model from dependencies to runtime</span>
<div class="axial-coord">
<div class="axial-coord-col axial-coord-col--left">
<span class="axial-coord-label">Typed environment</span>
<div class="coord-row"><span class="coord-pill">Application record</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Clock</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">HTTP</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">File system</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Process</span><span class="coord-line"></span></div>
<div class="coord-row"><span class="coord-pill">Your services</span><span class="coord-line"></span></div>
</div>
<div class="axial-coord-mid">
<div class="coord-hub">
<span class="coord-hub-logo">
<img class="hero-lockup hero-lockup--light" data-theme-variant="light" src="content/img/hero-lockup-light.png" alt="Axial" width="1560" height="600" />
<img class="hero-lockup hero-lockup--dark" data-theme-variant="dark" src="content/img/hero-lockup-dark.png" alt="Axial" width="1560" height="600" />
</span>
<span>Flow&lt;'env, 'error, 'value&gt;</span>
</div>
</div>
<div class="axial-coord-col axial-coord-col--right">
<span class="axial-coord-label">Supplied at the edge</span>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Live services</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Test doubles</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">.NET</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">NativeAOT</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Browser</span></div>
<div class="coord-row"><span class="coord-line"></span><span class="coord-pill">Node</span></div>
</div>
</div>
<p class="axial-coord-caption">A workflow names the environment it needs. The caller supplies the implementation when it runs the workflow.</p>
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
