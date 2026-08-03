namespace Axial.Refined.Tests

open Axial.Constraint
open Axial.Refined
open Swensen.Unquote
open Xunit

/// Proves the relationship an interval encodes, and that emptiness is reported as an
/// option rather than smuggled into a second interval type.
module IntervalTests =

    let private oneToFive () = Interval.between 1 5

    [<Fact>]
    let ``between is total and orders its arguments`` () =
        test <@ Interval.between 1 5 = Interval.between 5 1 @>
        test <@ (Interval.between 5 1).Lower = 1 @>
        test <@ (Interval.between 5 1).Upper = 5 @>

    [<Fact>]
    let ``create rejects inverted bounds that between would have silently repaired`` () =
        test <@ Interval.create 1 5 |> Result.map Interval.toPair = Ok(1, 5) @>
        test <@ Interval.create 5 1 |> Result.isError @>

    [<Fact>]
    let ``singleton intervals contain exactly their value`` () =
        let point = Interval.singleton 3
        test <@ Interval.isSingleton point @>
        test <@ Interval.contains 3 point @>
        test <@ not (Interval.contains 4 point) @>

    [<Fact>]
    let ``contains is inclusive on both bounds`` () =
        let interval = oneToFive ()
        test <@ Interval.contains 1 interval @>
        test <@ Interval.contains 5 interval @>
        test <@ not (Interval.contains 0 interval) @>
        test <@ not (Interval.contains 6 interval) @>

    [<Fact>]
    let ``intersect returns None for disjoint intervals rather than an empty interval`` () =
        test <@ Interval.intersect (Interval.between 1 5) (Interval.between 7 9) = None @>
        test <@ Interval.intersect (Interval.between 1 5) (Interval.between 3 9) |> Option.map Interval.toPair = Some(3, 5) @>

    [<Fact>]
    let ``intersect is commutative and idempotent`` () =
        let a = Interval.between 1 5
        let b = Interval.between 3 9
        test <@ Interval.intersect a b = Interval.intersect b a @>
        test <@ Interval.intersect a a = Some a @>

    [<Fact>]
    let ``touching intervals overlap because the bounds are inclusive`` () =
        test <@ Interval.overlaps (Interval.between 1 3) (Interval.between 3 5) @>
        test <@ Interval.intersect (Interval.between 1 3) (Interval.between 3 5) |> Option.map Interval.toPair = Some(3, 3) @>
        test <@ not (Interval.overlaps (Interval.between 1 3) (Interval.between 4 5)) @>

    [<Fact>]
    let ``union refuses to invent a gap, while span closes it deliberately`` () =
        let first = Interval.between 1 2
        let second = Interval.between 8 9
        test <@ Interval.union first second = None @>
        test <@ Interval.span first second |> Interval.toPair = (1, 9) @>
        test <@ Interval.union first (Interval.between 2 9) |> Option.map Interval.toPair = Some(1, 9) @>

    [<Fact>]
    let ``clamp is total and idempotent`` () =
        let interval = oneToFive ()
        test <@ Interval.clamp 0 interval = 1 @>
        test <@ Interval.clamp 3 interval = 3 @>
        test <@ Interval.clamp 9 interval = 5 @>

        let once = Interval.clamp 9 interval
        test <@ Interval.clamp once interval = once @>

    [<Fact>]
    let ``a clamped value always satisfies contains`` () =
        let interval = oneToFive ()
        for candidate in [ -10; 0; 1; 3; 5; 6; 100 ] do
            test <@ Interval.contains (Interval.clamp candidate interval) interval @>

    [<Fact>]
    let ``mapMonotonic survives an order-reversing mapping`` () =
        let negated = Interval.mapMonotonic (fun value -> -value) (oneToFive ())
        test <@ Interval.toPair negated = (-5, -1) @>

    [<Fact>]
    let ``containsInterval and span agree with each other`` () =
        let inner = Interval.between 2 3
        let outer = Interval.between 1 5
        test <@ Interval.containsInterval inner outer @>
        test <@ not (Interval.containsInterval outer inner) @>
        test <@ Interval.span inner outer = outer @>

    [<Fact>]
    let ``an interval spans every value it was built from`` () =
        let values = NonEmpty(4, [ -2; 9; 0 ])
        let interval = Interval.ofNonEmptyList values
        test <@ Interval.toPair interval = (-2, 9) @>
        test <@ values |> NonEmptyList.forall (fun value -> Interval.contains value interval) @>

    [<Fact>]
    let ``intervals work over any comparable, not only numbers`` () =
        let january = System.DateTime(2026, 1, 1)
        let december = System.DateTime(2026, 12, 31)
        let year = Interval.between january december
        test <@ Interval.contains (System.DateTime(2026, 6, 1)) year @>
        test <@ not (Interval.contains (System.DateTime(2025, 6, 1)) year) @>
        test <@ Interval.between "apple" "pear" |> Interval.contains "banana" @>

    [<Fact>]
    let ``the derived constraint admits exactly the interval's values`` () =
        let interval = oneToFive ()
        let check = interval |> Interval.toConstraint |> Constraint.check
        test <@ check 3 = Ok() @>
        test <@ check 9 |> Result.isError @>

    [<Fact>]
    let ``between accepts either order while create rejects an inverted pair`` () =
        // The ends name bounds, not a traversal, so between has no preferred order.
        test <@ Interval.between 5 1 = Interval.between 1 5 @>
        test <@ Interval.create 1 5 |> Result.isOk @>
        test <@ Interval.create 5 1 |> Result.isError @>

    [<Fact>]
    let ``a date range is the same type as any other interval`` () =
        let january = System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero)
        let december = System.DateTimeOffset(2026, 12, 31, 0, 0, 0, System.TimeSpan.Zero)
        let year : DateRange = Interval.between january december

        test <@ Interval.contains (System.DateTimeOffset(2026, 6, 1, 0, 0, 0, System.TimeSpan.Zero)) year @>
        test <@ Interval.lower year = january @>
        test <@ Interval.upper year = december @>

    [<Fact>]
    let ``float is not totally ordered, so NaN needs FiniteFloat rather than between`` () =
        // NaN compares false against everything, so between cannot order it. This is
        // documented rather than silently trusted.
        let broken = Interval.between nan 1.0
        test <@ not (Interval.lower broken <= Interval.upper broken) @>

        // create rejects the pair instead.
        test <@ Interval.create nan 1.0 |> Result.isError @>

        // FiniteFloat removes the problem at the type level: NaN cannot be admitted.
        test <@ FiniteFloat.create nan |> Result.isError @>

        let low = FiniteFloat.create 1.0 |> Result.defaultWith (failwithf "%A")
        let high = FiniteFloat.create 5.0 |> Result.defaultWith (failwithf "%A")
        let sound = Interval.between high low
        test <@ Interval.lower sound <= Interval.upper sound @>
        test <@ Interval.contains (FiniteFloat.create 3.0 |> Result.defaultWith (failwithf "%A")) sound @>

    [<Fact>]
    let ``width and duration answer the obvious question about a range`` () =
        test <@ Interval.widthInt (Interval.between 3 10) = 7L @>
        test <@ Interval.widthDecimal (Interval.between 1.5m 4.0m) = 2.5m @>

        let from = System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero)
        let until = from.AddDays 7.0
        test <@ Interval.duration (Interval.between from until) = System.TimeSpan.FromDays 7.0 @>

    [<Fact>]
    let ``integer width widens rather than overflowing`` () =
        // Int32.MaxValue - Int32.MinValue does not fit an int, so the width is 64-bit.
        test <@ System.Int32.MaxValue - System.Int32.MinValue = -1 @>

        let widest = Interval.between System.Int32.MinValue System.Int32.MaxValue
        test <@ Interval.widthInt widest = 4294967295L @>
