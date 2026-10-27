namespace Axial.ElmishFlows

open Axial

module internal Diagnostics =
    let report (options: ElmishRuntimeOptions) defect =
        // A failing diagnostic sink has no further safe destination; never fail the runtime job.
        try options.OnDefect defect with _ -> () // axial-allow-AXG010: The defect sink is the last reporting destination; its failure must not recursively fail the runtime job.

    let exceptionFrom name options (error: exn) =
        let rendered =
            try error.GetType().Name + ": " + error.Message
            with _ -> "Host callback failed" // axial-allow-AXG010: Rendering failure falls back to text; the original exception is still passed to report below.
        report options { Name = name; Error = Some error; Rendered = rendered }

    let cause name options cause =
        let rendered =
            try Cause.prettyPrint (fun error -> match box error with null -> "null" | value -> value.GetType().Name) cause
            with _ -> "Cause rendering failed" // axial-allow-AXG010: Cause rendering failure uses fallback text; each original defect is still sent to report below.
        for defect in Cause.defects cause do
            report options { Name = name; Error = Some defect; Rendered = rendered }
