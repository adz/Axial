---
title: "Scope"
linkTitle: "Scope"
weight: 1000
type: docs
---


 Owns finalizers for resources acquired during provisioning or runtime execution.


## Signature

<div class="fsdocs-usage">
<code>type Scope</code>
</div>

## Remarks


 Scopes aggregate cleanup in reverse registration order, prevent double-disposal, and surface
 cleanup failures as defects rather than typed business errors.
