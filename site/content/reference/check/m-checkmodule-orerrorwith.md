---
title: "FsFlow.CheckModule.orErrorWith"
linkTitle: "orErrorWith`"
type: docs
---

Maps a unit error into an application error produced on demand.



## Parameters

- `errorFn`: A function of type `unit -> 'error` to produce the error.
- `result`: The source `Check`.

## Returns

A `Result` with the produced error value.

