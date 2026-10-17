namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module TimeProviderTests =
    [<Fact>]
    let ``fromTimeProvider reads the provider's time`` () =
        let clock = Clock.fromTimeProvider TimeProvider.System

        test <@ abs ((clock.UtcNow() - DateTimeOffset.UtcNow).TotalMilliseconds) < 1000.0 @>
        test <@ clock.Elapsed() >= TimeSpan.Zero @>

    [<Fact>]
    let ``toTimeProvider exposes a fixed clock`` () =
        let fixedTime = DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)
        let provider = Clock.toTimeProvider (Clock.fromValue fixedTime)

        test <@ provider.GetUtcNow() = fixedTime @>
        test <@ provider.GetElapsedTime(provider.GetTimestamp()) = TimeSpan.Zero @>

    [<Fact>]
    let ``toTimeProvider timer fires after the due time`` () =
        let provider = Clock.toTimeProvider Clock.live
        let fired = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let timer =
            provider.CreateTimer(
                (fun _ -> fired.TrySetResult() |> ignore),
                null,
                TimeSpan.FromMilliseconds 20.0,
                Timeout.InfiniteTimeSpan)

        test <@ fired.Task.Wait(TimeSpan.FromSeconds 5.0) @>
        timer.Dispose()
