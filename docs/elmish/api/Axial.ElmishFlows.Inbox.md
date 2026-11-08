---
title: Inbox
api:
  family: Axial.ElmishFlows.Inbox
  sections:
    - id: create
      title: Create inboxes
      order: 10
      summary: Build the mailbox in the scope that will shut it down, naming its overflow strategy.
      members: ["scopedWith", "scopedSliding", "scopedDropping", "scopedBounded", "scopedUnbounded", "create"]
      facets:
        task: [create]
    - id: offer
      title: Offer values
      order: 20
      summary: Hand values to the program from `update` or from a synchronous callback.
      members: ["offer", "post"]
      facets:
        task: [offer]
    - id: read
      title: Read the mailbox
      order: 30
      summary: Read values as a stream and count the offers a full mailbox could not keep.
      members: ["stream", "dropped", "Dropped"]
      facets:
        task: [read]
---

# Inbox

`Inbox<'value>` is one-way, drop-visible mail into an Elmish program: one queue, one reader, owned by a scope. Use it
for values that arrive from outside `update` — a host callback, another library's event, a search box whose input must
never block the UI thread. For work distribution or back-pressure between fibers, use
[`Queue`](/api/Axial.QueueModule.html) directly.

## Create inboxes

Build the inbox in the scope that shuts it down. A layer given to `ElmishRuntime.startLayer` is provisioned in the
runtime's root scope, so an inbox built there stays open for the program and is shut down with the runtime. The
environment field is filled with `let! commands = Inbox.scopedSliding 1` inside the layer's `flow { }`, and nothing has to
register the inbox with the runtime afterwards.

[`scopedWith`](#Axial.ElmishFlows.Inbox.scopedWith) takes the `QueueStrategy` itself. The named constructors are the
four strategies:

- [`scopedSliding capacity`](#Axial.ElmishFlows.Inbox.scopedSliding) keeps the newest pending value and evicts the
  oldest, so a burst collapses to the latest request.
- [`scopedDropping capacity`](#Axial.ElmishFlows.Inbox.scopedDropping) keeps the oldest pending value and refuses a
  newer offer.
- [`scopedBounded capacity`](#Axial.ElmishFlows.Inbox.scopedBounded) waits while the mailbox is full, so every value is
  delivered. Do not offer to it from a `Cmd`: a command runs on the dispatch thread and would wait there. Offer from a
  fiber instead.
- [`scopedUnbounded()`](#Axial.ElmishFlows.Inbox.scopedUnbounded) never waits and grows with the backlog.

[`create capacity runtime`](#Axial.ElmishFlows.Inbox.create) remains for a host that already holds the runtime. It drops
the newest offer (`QueueStrategy.Dropping`) and shuts the inbox down when the runtime stops.

## Offer values

- [`offer value inbox`](#Axial.ElmishFlows.Inbox.offer) is an Elmish command: `update` returns it and the offer reaches
  the mailbox in dispatch order.
- [`post value inbox`](#Axial.ElmishFlows.Inbox.post) offers immediately and reports whether the mailbox accepted the
  value, for a synchronous callback that cannot return a command.

## Read the mailbox

[`stream inbox`](#Axial.ElmishFlows.Inbox.stream) is the inbox as a `FlowStream`: every accepted value, in the order it
was offered. Read it once, because two readers split the mail between them. Pipe it through `FlowStream.debounce` and
`FlowStream.switchMapFlow` for search-as-you-type, then run it with `Sub.OfFlowStream.subscribe`.

[`dropped inbox`](#Axial.ElmishFlows.Inbox.dropped) counts the offers a full mailbox could not keep. Surface it next to
the search status so a coalesced search is visible instead of silent.
