---
title: Queue
api:
  family: Axial.QueueModule
  sections:
    - id: create
      title: Create queues
      order: 10
      members: ["make", "makeScoped", "bounded", "dropping", "sliding", "unbounded"]
      facets:
        task: [create]
    - id: offer
      title: Offer values
      order: 20
      members: ["offer", "tryOffer", "offerAll"]
      facets:
        task: [offer]
    - id: callbacks
      title: Use from callbacks
      order: 30
      members: ["droppingNow", "shutdownNow"]
      facets:
        task: [callbacks]
---
# Queue

`Queue<'a>` hands each value from producer fibers to exactly one taker, in FIFO order, with an overflow strategy chosen
when it is created. Several fibers can take from one queue and share its values between them. This module creates
queues and offers to them; take values, inspect, and shut a queue down with the
[`Dequeue`](/api/Axial.DequeueModule.html) functions, which accept a `Queue`.

Create a queue with [`make`](#Axial.Queue.make) and a `QueueStrategy`, or with a shorthand:
[`bounded`](#Axial.Queue.bounded) suspends producers while full, [`dropping`](#Axial.Queue.dropping) discards the new
value, [`sliding`](#Axial.Queue.sliding) evicts the oldest, and [`unbounded`](#Axial.Queue.unbounded) never refuses.
[`makeScoped`](#Axial.Queue.makeScoped) shuts the queue down when the current scope closes.

Interrupting a suspended [`offer`](#Axial.Queue.offer) never enqueues its value, and interrupting a suspended take never
loses one. When zero or many consumers must each see every value, use a [`Hub`](/api/Axial.HubModule.html) instead.

[`tryOffer`](#Axial.Queue.tryOffer) returns immediately and can be called from a synchronous callback. Its
`QueueTryOfferResult` distinguishes acceptance, a full back-pressure queue, discard, eviction, and shutdown. A `Full`
or `Shutdown` result leaves the offered value outside the queue.

Read the [Queue guide](/concurrency-and-state/queue.html) for strategies, batching, shutdown, and draining on stop.
