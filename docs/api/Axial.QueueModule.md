# Queue

`Queue<'a>` hands values from producer fibers to one logical consumer, in FIFO order, with an overflow strategy chosen
when it is created. This module creates queues and offers to them; take values, inspect, and shut a queue down with the
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
