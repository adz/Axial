Goal: Design and implement the minimal FsFlow v1 concurrency primitives.

Objective:
Add only the concurrency primitives that materially improve on existing .NET TPL primitives by carrying FsFlow
semantics: typed `Exit`/`Cause`, structured interruption, scoped release safety, and composition with `Flow.fork`,
`Flow.join`, `Flow.race`, `Flow.timeout`, and `Flow.provide`.

Recommendation:
- Add a small one-shot deferred primitive.
- Add a small Flow-native semaphore API.
- Defer queues for v1 unless a concrete feature needs FsFlow-owned backpressure and shutdown semantics.

Rationale:
- .NET already has `TaskCompletionSource`, `SemaphoreSlim`, `Channel<T>`, `ConcurrentQueue<T>`, and related primitives.
- FsFlow should wrap a primitive only when it adds typed outcomes, cancellation/interruption behavior, scope safety, or
  better Flow composition.
- A deferred value and semaphore pass that bar with a small API surface.
- Queues do not pass that bar yet because a serious FsFlow queue implies a larger design: bounded strategy, fairness,
  shutdown, blocked taker/offerer cancellation, interruption, resource cleanup, and typed failure behavior.

Scope:
1. Deferred / Promise.
   - Choose the public name before implementation. Prefer `Deferred<'error, 'value>` unless comparison with ZIO/Effect
     strongly favors `Promise`.
   - Model a one-shot completion with `Exit<'value, 'error>`.
   - Provide creation inside `Flow`.
   - Provide await/get as `Flow<_, 'error, 'value>`.
   - Provide complete/succeed/fail/die/interrupt operations.
   - Completion must be idempotent or explicitly report already-completed; decide before implementation.
   - Awaiters must unblock on completion.
   - Awaiting must respect Flow cancellation/interruption.

2. Semaphore.
   - Back with `SemaphoreSlim` on .NET unless tests expose a semantic mismatch.
   - Provide `make`/`create` and `withPermit`.
   - Keep raw acquire/release APIs internal or secondary; prefer scoped `withPermit` so permits are always released on
     success, typed failure, defect, or interruption.
   - Ensure cancellation while waiting does not leak permits.
   - Ensure defects inside the protected flow release permits.

3. Queues.
   - Do not implement full queues in this goal by default.
   - Record a TODO for future queue design if the implementation uncovers a concrete need.
   - If queues are later needed, design them around explicit backpressure and shutdown semantics rather than thinly
     re-exporting `Channel<T>`.

Non-goals:
- Do not add hubs/pub-sub.
- Do not add STM transactional queues.
- Do not add a broad concurrency package by wrapping every .NET primitive.
- Do not redesign `Flow`, `Fiber`, or runtime scheduling unless the minimal primitives cannot be correct otherwise.
- Do not pursue full ZIO parity beyond the selected v1 primitives.

Acceptance:
- Public APIs are small, idiomatic F#, and consistent with existing FsFlow naming.
- Deferred and semaphore compose with `Flow.fork`, `Flow.join`, `Flow.race`, `Flow.timeout`, and cancellation.
- Tests cover success, typed failure, defect, interruption/cancellation, blocked waiters, and release/finalizer behavior.
- Queue work is either explicitly deferred in `TODO.md` or justified by a concrete dependency found during implementation.
- Docs and API reference are updated only for APIs actually added.
- `dotnet test` passes.
- `dotnet build FsFlow.slnx` passes.
- `bash scripts/check-fable-js-surface.sh` passes or unsupported APIs are correctly guarded from the Fable surface.
- `bash scripts/generate-api-docs.sh` passes without unresolved-symbol warnings.
- `npm run build` in `site` passes.
- `bash scripts/preview-docs.sh` reaches Hugo startup.
- Commit the completed work.
