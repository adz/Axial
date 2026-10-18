# Cmd

`Axial.ElmishFlows.Cmd` turns Axial workflows into Elmish commands. Each command runs in the runtime's scope, off the
dispatch thread. Read the [Elmish guide](/platforms-and-hosting/elmish.html) for the first-use lifecycle example.

## Command operations

- `Cmd.OfFlow.either name runtime workflow onSuccess onError` dispatches a success or typed failure as a message.
- `Cmd.OfFlow.perform` is for workflows without typed failures.
- `Cmd.OfFlow.attempt` handles typed failures of unit-valued workflows.

Interruptions send no message.

## Latest-wins commands

`Cmd.OfFlow.Latest.either key runtime workflow onSuccess onError` starts a latest-wins request: starting another
command with the same key interrupts the previous one and suppresses its queued delivery. Keys belong to the runtime,
not to the process. `Cmd.OfFlow.Latest.cancel key runtime` invalidates a pending request.
