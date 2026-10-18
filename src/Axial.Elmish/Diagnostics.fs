namespace Axial.ElmishFlows

open Axial

module internal Diagnostics =
    let report (options: ElmishRuntimeOptions) defect =
        // A failing diagnostic sink has no further safe destination; never fail the runtime job.
        try options.OnDefect defect with _ -> ()

    let exceptionFrom name options (error: exn) =
        let rendered =
            try error.GetType().Name + ": " + error.Message
            with _ -> "Host callback failed"
        report options { Name = name; Error = Some error; Rendered = rendered }

    let cause name options cause =
        let rendered =
            try Cause.prettyPrint (fun error -> match box error with null -> "null" | value -> value.GetType().Name) cause
            with _ -> "Cause rendering failed"
        for defect in Cause.defects cause do
            report options { Name = name; Error = Some defect; Rendered = rendered }
