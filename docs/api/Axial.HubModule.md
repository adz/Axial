# Hub

`Hub<'a>` delivers every published value to every current subscription. Each subscription chooses its own
`QueueStrategy`, so one publisher can feed a lossless consumer and latest-value consumers at the same time, and every
subscriber sees values in the same order.

[`subscribe`](#Axial.Hub.subscribe) returns the subscription as a [`Dequeue`](/api/Axial.DequeueModule.html) owned by
the current scope; to subscribe for the life of a stream, use
[`FlowStream.fromHub`](/api/Axial.FlowStreamModule.html#Axial.FlowStream.fromHub). [`publish`](#Axial.Hub.publish)
waits while a `BackPressure` subscription is full, and no later value reaches any subscriber meanwhile;
[`tryPublish`](#Axial.Hub.tryPublish) publishes only when no waiting is needed, to every subscription or to none.
[`shutdown`](#Axial.Hub.shutdown) lets every subscriber drain its backlog.

A subscriber that joins late sees only later values; for a current value followed by every change, use a
[`SubscriptionRef`](/api/Axial.State.SubscriptionRefModule.html). Read the [Hub guide](/concurrency-and-state/hub.html),
and the [torture test](/concurrency-and-state/torture-test.html) for every guarantee checked together.
