# SubscriptionRef

`SubscriptionRef<'a>` is a reference whose changes can be streamed. [`changes`](#Axial.State.SubscriptionRef.changes)
starts with the value current when the stream starts, then delivers every later update, with no gap and no duplicate
between the two, buffered by a `QueueStrategy` chosen per stream.

Use it for state that consumers join at different times, such as a display opened after a feed started. Update it with
[`set`](#Axial.State.SubscriptionRef.set), [`update`](#Axial.State.SubscriptionRef.update), or
[`modify`](#Axial.State.SubscriptionRef.modify); each update reaches every running stream before the next starts.

Read the [SubscriptionRef guide](/concurrency-and-state/subscription-ref.html).
