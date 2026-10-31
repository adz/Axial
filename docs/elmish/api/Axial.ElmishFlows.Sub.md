---
title: Sub
api:
  family: Axial.ElmishFlows.Sub
---

# Sub

`Axial.ElmishFlows.Sub` connects `FlowStream` subscriptions to an Elmish program.

`Sub.OfFlowStream.subscribe id runtime stream onError` runs a stream while Elmish keeps the subscription id. Removing
the id disposes it, interrupts its fiber, and runs scoped finalizers. Delivery still passes through `Post`.
