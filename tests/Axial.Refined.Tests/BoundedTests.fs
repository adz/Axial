namespace Axial.Refined.Tests

open Axial.Refined
open Swensen.Unquote
open Xunit

/// Proves that a bounded value cannot leave its range, including through mapping.
module BoundedTests =

    let private percent () = Interval.between 0 100

    [<Fact>]
    let ``clamp is total, so an out-of-range input still yields a bounded value`` () =
        let bounds = percent ()
        test <@ (Bounded.clamp bounds 150).Value = 100 @>
        test <@ (Bounded.clamp bounds -20).Value = 0 @>
        test <@ (Bounded.clamp bounds 42).Value = 42 @>

    [<Fact>]
    let ``create rejects what clamp would have repaired`` () =
        let bounds = percent ()
        test <@ Bounded.create bounds 42 |> Result.map Bounded.value = Ok 42 @>
        test <@ Bounded.create bounds 150 |> Result.isError @>

    [<Fact>]
    let ``a bounded value always lies within its own bounds`` () =
        let bounds = percent ()
        for candidate in [ -100; 0; 50; 100; 1000 ] do
            let bounded = Bounded.clamp bounds candidate
            test <@ Interval.contains bounded.Value bounded.Bounds @>

    [<Fact>]
    let ``map re-clamps, so a mapping that leaves the range cannot break the invariant`` () =
        let bounded = Bounded.clamp (percent ()) 90
        let doubled = Bounded.map ((*) 2) bounded
        test <@ doubled.Value = 100 @>
        test <@ Interval.contains doubled.Value doubled.Bounds @>

    [<Fact>]
    let ``map keeps the original bounds rather than widening them`` () =
        let bounds = percent ()
        let mapped = Bounded.map ((*) 5) (Bounded.clamp bounds 50)
        test <@ mapped.Bounds = bounds @>

    [<Fact>]
    let ``withBounds moves a value into a new range, clamping as needed`` () =
        let bounded = Bounded.clamp (percent ()) 80
        let narrowed = Bounded.withBounds (Interval.between 0 50) bounded
        test <@ narrowed.Value = 50 @>

    [<Fact>]
    let ``normalize reports position within the bounds as a fraction`` () =
        let bounds = Interval.between 0.0 200.0
        test <@ BoundedFloat.normalize (Bounded.clamp bounds 50.0) = 0.25 @>
        test <@ BoundedFloat.normalize (Bounded.clamp bounds 0.0) = 0.0 @>
        test <@ BoundedFloat.normalize (Bounded.clamp bounds 200.0) = 1.0 @>

    [<Fact>]
    let ``normalize returns zero for degenerate bounds instead of dividing by zero`` () =
        let point = Interval.singleton 5.0
        test <@ BoundedFloat.normalize (Bounded.clamp point 5.0) = 0.0 @>

    [<Fact>]
    let ``normalize and denormalize round-trip within the bounds`` () =
        let bounds = Interval.between 10.0 20.0
        let bounded = Bounded.clamp bounds 17.5
        test <@ BoundedFloat.denormalize bounds (BoundedFloat.normalize bounded) = bounded @>

    [<Fact>]
    let ``a bounded value can be admitted through its refinement`` () =
        let bounds = percent ()
        let refinement = Bounded.refinement bounds

        test <@ Axial.Refined.Refinement.create refinement 42 |> Result.map Bounded.value = Ok 42 @>
        test <@ Axial.Refined.Refinement.create refinement 150 |> Result.isError @>
        test <@ Refine.bounded bounds 42 |> Result.map Bounded.value = Ok 42 @>
