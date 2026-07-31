namespace Axial.Refined.Tests

open System
open Axial.Refined
open Swensen.Unquote
open Xunit

/// Proves what Finite actually guarantees (lawful ordering) and what it does not
/// (safe arithmetic), plus the one closed multiplication in the package.
module FiniteAndUnitTests =

    let private finite value =
        FiniteFloat.create value |> Result.defaultWith (failwithf "%A")

    let private unit' value =
        UnitInterval.create value |> Result.defaultWith (failwithf "%A")

    [<Fact>]
    let ``NaN and infinities are rejected at admission`` () =
        test <@ FiniteFloat.create 1.5 |> Result.map FiniteFloat.value = Ok 1.5 @>
        test <@ FiniteFloat.create Double.NaN |> Result.isError @>
        test <@ FiniteFloat.create Double.PositiveInfinity |> Result.isError @>
        test <@ FiniteFloat.create Double.NegativeInfinity |> Result.isError @>
        test <@ FiniteFloat.ofFloat Double.NaN = None @>

    [<Fact>]
    let ``the defect is a silently poisoned aggregate, not a broken sort`` () =
        // One bad reading destroys the whole result, with no exception and no obviously
        // wrong number. This is what admitting through FiniteFloat prevents.
        let readings = [ 12.5; 3.0; Double.NaN; 8.25 ]
        test <@ Double.IsNaN(List.sum readings) @>
        test <@ Double.IsNaN(List.average readings) @>

        // Infinity poisons them identically, which is why the type excludes it too.
        test <@ Double.IsInfinity(List.sum [ 12.5; infinity ]) @>

        test <@ FiniteFloat.create Double.NaN |> Result.isError @>
        test <@ FiniteFloat.average (NonEmpty(finite 12.5, [ finite 3.0 ])) |> Result.isOk @>

    [<Fact>]
    let ``sorting and map keys already work on plain float, so they are not the reason`` () =
        // F# generic comparison orders NaN consistently: it is not the IEEE operator.
        test <@ compare Double.NaN Double.NaN = 0 @>
        test <@ compare Double.NaN 1.0 = -1 @>
        // Asserted positionally: comparing lists that contain NaN with = is itself
        // unreliable, because list equality uses IEEE equality element by element.
        let sorted = List.sort [ 1.0; Double.NaN; 2.0 ]
        test <@ Double.IsNaN(List.head sorted) @>
        test <@ List.tail sorted = [ 1.0; 2.0 ] @>
        test <@ Map.tryFind Double.NaN (Map [ Double.NaN, "x" ]) = Some "x" @>
        test <@ Set.contains Double.NaN (Set.ofList [ Double.NaN ]) @>

    [<Fact>]
    let ``IEEE equality is what breaks, so contains and distinct go wrong`` () =
        test <@ not (Double.NaN = Double.NaN) @>
        test <@ not (List.contains Double.NaN [ Double.NaN ]) @>
        test <@ List.distinct [ Double.NaN; Double.NaN ] |> List.length = 2 @>

        // A comparison written with < and > reports NaN equal to everything, which is
        // intransitive and leaves sortWith returning unsorted output without raising.
        let naive (a: float) (b: float) = if a < b then -1 elif a > b then 1 else 0
        test <@ naive Double.NaN 1.0 = 0 && naive Double.NaN 2.0 = 0 && naive 1.0 2.0 = -1 @>
        test <@ List.sortWith naive [ 3.0; Double.NaN; 1.0; 2.0 ] <> [ Double.NaN; 1.0; 2.0; 3.0 ] @>

        // Excluded at admission, so none of the above can arise.
        let values = [ finite 3.0; finite 1.0 ]
        test <@ List.contains (finite 3.0) values @>
        test <@ List.distinct [ finite 1.0; finite 1.0 ] |> List.length = 1 @>

    [<Fact>]
    let ``negate and abs are closed, so they return a FiniteFloat directly`` () =
        test <@ FiniteFloat.negate (finite 2.5) = finite -2.5 @>
        test <@ FiniteFloat.abs (finite -2.5) = finite 2.5 @>
        test <@ FiniteFloat.negate (FiniteFloat.negate (finite 2.5)) = finite 2.5 @>



    [<Fact>]
    let ``average divides before summing, so a list that would overflow still averages`` () =
        let big = finite Double.MaxValue
        test <@ FiniteFloat.average (NonEmpty(finite 1.0, [ finite 2.0; finite 3.0 ])) |> Result.map FiniteFloat.value = Ok 2.0 @>
        test <@ FiniteFloat.sum (NonEmpty(big, [ big ])) |> Result.isError @>
        test <@ FiniteFloat.average (NonEmpty(big, [ big ])) |> Result.isOk @>

    [<Fact>]
    let ``arithmetic happens on plain floats, with one admission at the end`` () =
        // The type does not thread a Result through every step: F# cannot propagate the
        // invariant through arithmetic, so that would cost more than it returns.
        let a = finite 2.5
        let b = finite 4.0
        test <@ FiniteFloat.create (FiniteFloat.value a * FiniteFloat.value b) |> Result.map FiniteFloat.value = Ok 10.0 @>
        test <@ FiniteFloat.create (FiniteFloat.value a / 0.0) |> Result.isError @>

    [<Fact>]
    let ``minimum and maximum over a non-empty list are total`` () =
        let values = NonEmpty(finite 3.0, [ finite -1.0; finite 2.0 ])
        test <@ FiniteFloat.minimum values = finite -1.0 @>
        test <@ FiniteFloat.maximum values = finite 3.0 @>

    [<Fact>]
    let ``unit interval admits only zero to one inclusive`` () =
        test <@ UnitInterval.create 0.0 |> Result.isOk @>
        test <@ UnitInterval.create 1.0 |> Result.isOk @>
        test <@ UnitInterval.create 0.5 |> Result.map UnitInterval.value = Ok 0.5 @>
        test <@ UnitInterval.create 1.5 |> Result.isError @>
        test <@ UnitInterval.create -0.1 |> Result.isError @>
        test <@ UnitInterval.create Double.NaN |> Result.isError @>

    [<Fact>]
    let ``clamp is total and sends NaN to zero rather than admitting it`` () =
        test <@ UnitInterval.clamp 2.0 = UnitInterval.one @>
        test <@ UnitInterval.clamp -2.0 = UnitInterval.zero @>
        test <@ UnitInterval.clamp 0.25 = unit' 0.25 @>
        test <@ UnitInterval.clamp Double.NaN = UnitInterval.zero @>

    [<Fact>]
    let ``multiplication is closed, which is the reason the type exists`` () =
        for left in [ 0.0; 0.25; 0.5; 0.99; 1.0 ] do
            for right in [ 0.0; 0.3; 0.75; 1.0 ] do
                let product = UnitInterval.multiply (unit' left) (unit' right)
                test <@ UnitInterval.value product >= 0.0 && UnitInterval.value product <= 1.0 @>

    [<Fact>]
    let ``one is the multiplicative identity and zero is absorbing`` () =
        let value = unit' 0.4
        test <@ UnitInterval.multiply value UnitInterval.one = value @>
        test <@ UnitInterval.multiply value UnitInterval.zero = UnitInterval.zero @>

    [<Fact>]
    let ``complement is its own inverse for exactly representable values`` () =
        test <@ UnitInterval.complement (unit' 0.3) = unit' 0.7 @>
        test <@ UnitInterval.complement UnitInterval.zero = UnitInterval.one @>
        test <@ UnitInterval.complement UnitInterval.one = UnitInterval.zero @>

        for value in [ 0.0; 0.25; 0.5; 0.75; 1.0 ] do
            test <@ UnitInterval.complement (UnitInterval.complement (unit' value)) = unit' value @>

    [<Fact>]
    let ``complement is only an involution up to floating-point rounding`` () =
        // Documented rather than claimed: 1 - (1 - 0.3) is not 0.3 in IEEE 754, so the
        // round trip is exact for dyadic values and approximate for the rest.
        test <@ 1.0 - (1.0 - 0.3) <> 0.3 @>

        let value = unit' 0.3
        let roundTripped = UnitInterval.complement (UnitInterval.complement value)
        test <@ roundTripped <> value @>
        test <@ abs (UnitInterval.value roundTripped - UnitInterval.value value) < 1e-15 @>

    [<Fact>]
    let ``addition is deliberately absent, and saturatingAdd clamps instead`` () =
        // 0.7 + 0.7 leaves the range, so the type is not closed under addition.
        test <@ UnitInterval.saturatingAdd (unit' 0.7) (unit' 0.7) = UnitInterval.one @>
        test <@ UnitInterval.saturatingAdd (unit' 0.2) (unit' 0.3) = unit' 0.5 @>
        test <@ UnitInterval.saturatingSubtract (unit' 0.2) (unit' 0.9) = UnitInterval.zero @>

    [<Fact>]
    let ``lerp always lands between its endpoints`` () =
        test <@ UnitInterval.lerp 10.0 20.0 UnitInterval.zero = 10.0 @>
        test <@ UnitInterval.lerp 10.0 20.0 UnitInterval.one = 20.0 @>
        test <@ UnitInterval.lerp 10.0 20.0 UnitInterval.half = 15.0 @>

        for proportion in [ 0.0; 0.1; 0.5; 0.9; 1.0 ] do
            let result = UnitInterval.lerp 10.0 20.0 (unit' proportion)
            test <@ result >= 10.0 && result <= 20.0 @>

    [<Fact>]
    let ``inverseLerp and lerp are inverses through an interval`` () =
        let interval = Interval.between 10.0 20.0
        let proportion = UnitInterval.inverseLerpInterval interval 15.0
        test <@ proportion = UnitInterval.half @>
        test <@ UnitInterval.lerpInterval interval proportion = 15.0 @>

    [<Fact>]
    let ``inverseLerp clamps out-of-range values and survives degenerate bounds`` () =
        let interval = Interval.between 10.0 20.0
        test <@ UnitInterval.inverseLerpInterval interval 100.0 = UnitInterval.one @>
        test <@ UnitInterval.inverseLerpInterval interval 0.0 = UnitInterval.zero @>
        test <@ UnitInterval.inverseLerpInterval (Interval.singleton 5.0) 5.0 = UnitInterval.zero @>
        test <@ UnitInterval.inverseLerp 10.0 20.0 12.5 = unit' 0.25 @>

    [<Fact>]
    let ``one proportion convention: zero is the low end and one is the high end`` () =
        // blend used to sit beside lerp reading the proportion the opposite way round,
        // so blend 10 20 one gave 10 while lerp 10 20 one gave 20. Only lerp remains.
        test <@ UnitInterval.lerp 10.0 20.0 UnitInterval.zero = 10.0 @>
        test <@ UnitInterval.lerp 10.0 20.0 UnitInterval.one = 20.0 @>
        test <@ UnitInterval.inverseLerp 10.0 20.0 10.0 = UnitInterval.zero @>
        test <@ UnitInterval.inverseLerp 10.0 20.0 20.0 = UnitInterval.one @>

    [<Fact>]
    let ``single precision carries the same guarantees as double`` () =
        let single value = FiniteFloat32.create value |> Result.defaultWith (failwithf "%A")

        test <@ FiniteFloat32.create Single.NaN |> Result.isError @>
        test <@ FiniteFloat32.create Single.PositiveInfinity |> Result.isError @>
        test <@ List.sort [ single 3.0f; single 1.0f ] = [ single 1.0f; single 3.0f ] @>
        test <@ FiniteFloat32.negate (single 2.5f) = single -2.5f @>
        test <@ FiniteFloat32.maximum (NonEmpty(single 1.0f, [ single 9.0f ])) = single 9.0f @>
        test <@ FiniteFloat32.toFiniteFloat (single 1.5f) |> FiniteFloat.value = 1.5 @>

