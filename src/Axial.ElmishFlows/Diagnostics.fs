namespace Axial.ElmishFlows

open Axial

module internal Diagnostics =
    /// Waits for the fiber a runtime job forked and owns. Interrupting the job (the runtime stopping) stops the wait,
    /// not the fiber, so the job then stops the fiber itself and takes its outcome: a defect from the fiber's cleanup
    /// still reaches the job's report instead of being lost.
    let awaitOwned (fiber: Fiber<'error, 'value>) : Flow<'env, Never, Exit<'value, 'error>> =
        Fiber.awaitExit fiber |> Flow.foldCauseFlow Flow.ok (fun _ -> Fiber.interrupt fiber)

    let report (options: ElmishRuntimeOptions) defect =
        // A failing diagnostic sink has no further safe destination; never fail the runtime job.
        // axial-note swallowed-exn: OnDefect is the final reporting sink; its own failure cannot be sent back to it or fail the runtime job.
        try options.OnDefect defect with _ -> ()

    let exceptionFrom name options (error: exn) =
        let rendered =
            try error.GetType().Name + ": " + error.Message
            // axial-note swallowed-exn: a custom exception Message getter may throw; retain the original exception in Error and still deliver the diagnostic.
            with _ -> "Host callback failed"
        report options { Name = name; Error = Some error; Rendered = rendered }

    let private render cause =
        try Cause.prettyPrint (fun error -> match box error with null -> "null" | value -> value.GetType().Name) cause
        // axial-note swallowed-exn: rendering failure must not prevent original defects from reaching OnDefect below.
        with _ -> "Cause rendering failed"

    let cause name options cause =
        let rendered = render cause
        for defect in Cause.defects cause do
            report options { Name = name; Error = Some defect; Rendered = rendered }

    /// Reports every defect in a cause, and a typed failure no callback could take, unless the cause is a pure
    /// interruption. Interruption is how a command, stream, subscription or service is cancelled, so it is silent.
    let causeUnlessInterrupted name options (failure: Cause<'error>) =
        if not (List.isEmpty (Cause.defects failure)) then cause name options failure
        elif not (List.isEmpty (Cause.failures failure)) then
            report options { Name = name; Error = None; Rendered = render failure }
