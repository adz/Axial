---
title: "STM.atomically"
linkTitle: "atomically"
type: docs
---

<div class="fsdocs-usage">
<code><span>STM.atomically&#32;<span>transaction</span></span></code>
</div>


 Executes an STM transaction atomically within a flow while preserving retry/orElse coordination.
 

## Parameters

- `transaction`: <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-stm-1.html">STM</a>&lt;'T&gt;</span></code>
  The STM transaction to execute.

## Returns

A flow that performs the transaction and returns its result.

