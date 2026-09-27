# Dequeue

`Dequeue<'a>` is the consuming side of a queue. Every [`Queue`](/api/Axial.QueueModule.html) is a `Dequeue`, and
[`Hub.subscribe`](/api/Axial.HubModule.html#Axial.Hub.subscribe) returns a bare `Dequeue`, so one consumer works for
both, and a subscriber cannot offer into its own buffer.

[`take`](#Axial.Dequeue.take) waits for the next value; [`takeBetween`](#Axial.Dequeue.takeBetween) waits for a
minimum and takes a batch; [`poll`](#Axial.Dequeue.poll), [`takeUpTo`](#Axial.Dequeue.takeUpTo), and
[`takeAll`](#Axial.Dequeue.takeAll) never wait. Values leave in FIFO order even when takers are interrupted: waiting
takers are served one at a time, so a value given back by an interrupted taker returns before anything later is taken.

[`shutdown`](#Axial.Dequeue.shutdown) ends a queue without discarding its backlog: later takes drain what remains, and
on a hub subscription it unsubscribes. [`stats`](#Axial.Dequeue.stats) reads the backlog and the accepted, dropped, and
evicted counters for monitoring.

Consume a queue as a stream with [`FlowStream.fromDequeue`](/api/Axial.FlowStreamModule.html#Axial.FlowStream.fromDequeue).
The [Queue guide](/concurrency-and-state/queue.html) covers batching, shutdown, and monitoring.
