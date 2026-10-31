# Inbox

`Inbox<'value>` is a dropping queue for synchronous UI input, created by
[`Inbox.create capacity runtime`](#Axial.ElmishFlows.Inbox.create). It shuts down with the runtime.

## Operations

- `Inbox.offer value inbox` is an Elmish command.
- `Inbox.post value inbox` offers immediately and returns whether it was accepted.
- `Inbox.dropped inbox` reports how many values were dropped, for when input can outpace the consumer.
- `Inbox.stream inbox` exposes the inbox as a `FlowStream`. Pipe it through `FlowStream.debounce` and
  `FlowStream.switchMapFlow` for search-as-you-type, then subscribe to the resulting stream.
