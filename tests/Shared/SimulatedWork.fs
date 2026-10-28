namespace Axial.Tests

open System
open System.Diagnostics
open System.Threading

/// Linked into test projects that have no TestSupport.fs of their own. Axial.Tests/TestSupport.fs carries the same helpers.
module TestSupport =
    /// Polls a fact for at most the supplied duration; false means the failure deadline expired.
    let waitUntil (condition: unit -> bool) (deadline: TimeSpan) =
        if deadline < TimeSpan.Zero then
            invalidArg (nameof deadline) "A polling deadline must be finite and non-negative."

        let elapsed = Stopwatch.StartNew()
        let mutable ready = condition ()

        while not ready && elapsed.Elapsed < deadline do
            Thread.Sleep 1
            ready <- condition ()

        ready

    /// Blocks the calling thread for the duration to simulate work (a slow handler, a loaded pool).
    /// The one allowed Thread.Sleep for simulated work; never use it to wait for a fact.
    let occupyThread (duration: TimeSpan) = Thread.Sleep duration
