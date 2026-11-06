---
title: Cmd
api:
  family: Axial.ElmishFlows.Cmd
---

# Cmd

`Axial.ElmishFlows.Cmd` turns Axial workflows into Elmish commands. Each command runs in the runtime's scope, off the
dispatch thread. Read the [Elmish guide](/elmish/elmish.html) for the first-use lifecycle example.

## Command operations

- `Cmd.OfFlow.either name runtime workflow onSuccess onError` dispatches a success or typed failure as a message.
- `Cmd.OfFlow.perform` is for workflows without typed failures.
- `Cmd.OfFlow.attempt` handles typed failures of unit-valued workflows; success dispatches nothing.

Interruptions send no message.

## Latest-wins commands

`Cmd.OfFlow.Latest.either key runtime workflow onSuccess onError` starts a latest-wins request: starting another
command with the same key interrupts the previous one and suppresses its queued delivery. Keys belong to the runtime,
not to the process. `Cmd.OfFlow.Latest.cancel key runtime` invalidates a pending request.

## Streaming commands

`Cmd.OfFlowStream.latest key runtime stream onElement onError` dispatches every element of a `FlowStream` as a message,
in order, through `Post`. At most one element waits in `Post`: the stream pulls the next element only after `update`
has seen the previous one, so a fast stream cannot flood the dispatcher. It shares keys with `Cmd.OfFlow.Latest`: a
newer command or stream for the key interrupts the running stream, and the element it posted but `update` has not yet
seen is dropped. A typed failure dispatches `onError` after the elements before it; completion dispatches nothing. An
exception from `onElement` is reported to `OnDefect` and that element is skipped.
