namespace Axial.Tests

open System
open Axial.Constraint
open Swensen.Unquote
open Xunit

module ConstraintTests =

    let private violation constraint' value =
        match Constraint.check constraint' value with
        | Ok() -> failwith "Expected the constraint to reject the value."
        | Error violation -> violation

    let private expression constraint' = (Constraint.inspect constraint').Expression

    // ---------------------------------------------------------------------------------------------------
    // Execution
    // ---------------------------------------------------------------------------------------------------

    module Execution =
        [<Fact>]
        let ``one named constraint answers satisfaction, failure, and the unchanged value`` () =
            let retryCount = Constraint.between 0 10

            test <@ Constraint.test retryCount 3 @>
            test <@ Constraint.check retryCount 3 = Ok() @>
            test <@ Constraint.guard retryCount 3 = Ok 3 @>
            test <@ not (Constraint.test retryCount 42) @>
            test <@ Constraint.guard retryCount 42 = Error(violation retryCount 42) @>

        [<Fact>]
        let ``test and check agree for every built-in constructor and combinator`` () =
            let agrees (constraint': Constraint<'value>) (values: 'value list) =
                values
                |> List.forall (fun value ->
                    Constraint.test constraint' value = (Constraint.check constraint' value = Ok()))

            let text: Constraint<string> = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 4 ]

            let currency =
                Constraint.customWith "must be a supported currency" (fun code ->
                    if code = "AUD" then
                        Ok()
                    else
                        Error(Atomic(Expected(MembershipAtom(OneOf [ ConstraintValue.Text "AUD" ]), ConstraintValue.tryCreate code))))

            let agreements =
                [ agrees text [ null; ""; " "; "a"; "ab"; "abcde" ]
                  agrees Constraint.email [ null; "ada"; "ada@example.com" ]
                  agrees (Constraint.any (Constraint.equalTo -1) [ Constraint.atLeast 1 ]) [ -1; 0; 1; 5 ]
                  agrees (Constraint.notWith "must not be admin" (Constraint.equalTo "admin")) [ "admin"; "ada" ]
                  agrees (Constraint.custom "must be even" (fun value -> value % 2 = 0)) [ 1; 2 ]
                  agrees currency [ "AUD"; "NZD" ] ]

            test <@ agreements = List.replicate 6 true @>

        [<Fact>]
        let ``customWith invokes its callback exactly once per check`` () =
            let mutable calls = 0

            let constraint' =
                Constraint.customWith "must be positive" (fun value ->
                    calls <- calls + 1
                    if value > 0 then Ok() else Error(Atomic(Described "must be positive")))

            Constraint.check constraint' 1 |> ignore
            let afterSuccess = calls

            Constraint.check constraint' -1 |> ignore
            let afterFailure = calls

            test <@ afterSuccess = 1 && afterFailure = 2 @>

    // ---------------------------------------------------------------------------------------------------
    // Built-in vocabulary
    // ---------------------------------------------------------------------------------------------------

    module Vocabulary =
        [<Fact>]
        let ``present and blank are exact complements for every supported shape`` () =
            let complementary (present: Constraint<'value>) (blank: Constraint<'value>) (values: 'value list) =
                values |> List.forall (fun value -> Constraint.test present value <> Constraint.test blank value)

            let complements =
                [ complementary Constraint.present Constraint.blank [ null; ""; " "; "Ada" ]
                  complementary Constraint.present Constraint.blank [ None; Some 1 ]
                  complementary Constraint.present Constraint.blank [ ValueNone; ValueSome 1 ]
                  complementary Constraint.present Constraint.blank [ Nullable(); Nullable 1 ]
                  complementary Constraint.present Constraint.blank [ []; [ 1 ] ]
                  complementary Constraint.present Constraint.blank [ [||]; [| 1 |] ]
                  complementary Constraint.present Constraint.blank [ Map.empty; Map [ "a", 1 ] ] ]

            test <@ complements = List.replicate 7 true @>

        [<Fact>]
        let ``present rejects whitespace while minLength 1 accepts it`` () =
            let present: Constraint<string> = Constraint.present
            let minLength: Constraint<string> = Constraint.minLength 1

            test <@ not (Constraint.test present " ") @>
            test <@ Constraint.test minLength " " @>

        [<Fact>]
        let ``text cardinality counts code points rather than UTF-16 units`` () =
            let single: Constraint<string> = Constraint.length 1

            // "\U0001F600" is one code point stored as two UTF-16 units, and JSON Schema counts it once.
            test <@ Constraint.test single "\U0001F600" @>
            test <@ "\U0001F600".Length = 2 @>

        [<Fact>]
        let ``numeric is ASCII digits so the runtime rule and its export agree`` () =
            test <@ Constraint.test Constraint.numeric "345" @>
            test <@ not (Constraint.test Constraint.numeric "٣٤٥") @>

        [<Fact>]
        let ``optional permits absence, blank requires it, and present forbids it`` () =
            let optional: Constraint<string option> = Constraint.optional (Constraint.minLength 3)
            let blank: Constraint<string option> = Constraint.blank
            let present: Constraint<string option> = Constraint.present

            test <@ Constraint.test optional None @>
            test <@ Constraint.test optional (Some "Ada") @>
            test <@ not (Constraint.test optional (Some "A")) @>
            test <@ Constraint.test blank None && not (Constraint.test blank (Some "Ada")) @>
            test <@ not (Constraint.test present None) && Constraint.test present (Some "Ada") @>

        [<Fact>]
        let ``a content constraint fails on null rather than treating absence as satisfaction`` () =
            let length: Constraint<string> = Constraint.minLength 1

            test <@ violation length null = Atomic(Expected(CardinalityAtom(Cardinality.Minimum 1), None)) @>
            test <@ violation Constraint.email null = Atomic(Expected(FormatAtom Email, Some ConstraintValue.Null)) @>

        [<Fact>]
        let ``every built-in constructor inspects as its matching atom`` () =
            let atomOf constraint' =
                match expression constraint' with
                | ConstraintExpression.Atom atom -> atom
                | other -> failwithf "Expected an atom, but was %A." other

            let text: Constraint<string> = Constraint.present
            let size: Constraint<string> = Constraint.lengthBetween 2 4
            let tags: Constraint<string list> = Constraint.distinct
            let items: Constraint<int list> = Constraint.contains 1

            let atoms =
                [ atomOf text
                  atomOf size
                  atomOf Constraint.email
                  atomOf (Constraint.pattern "^a$")
                  atomOf (Constraint.oneOf [ "a"; "b" ])
                  atomOf (Constraint.equalTo 1)
                  atomOf (Constraint.notEqualTo 1)
                  atomOf (Constraint.greaterThan 1)
                  atomOf (Constraint.atMost 1)
                  atomOf (Constraint.between 1 2)
                  atomOf tags
                  atomOf items
                  atomOf (Constraint.multipleOf 5)
                  atomOf Constraint.finite ]

            test <@
                atoms =
                    [ PresenceAtom Present
                      CardinalityAtom(Cardinality.Between(2, 4))
                      FormatAtom Email
                      FormatAtom(Pattern "^a$")
                      MembershipAtom(OneOf [ ConstraintValue.Text "a"; ConstraintValue.Text "b" ])
                      RelationAtom(Compared(Equal, ConstraintValue.Integer 1L))
                      RelationAtom(Compared(NotEqual, ConstraintValue.Integer 1L))
                      RelationAtom(Compared(GreaterThan, ConstraintValue.Integer 1L))
                      RelationAtom(Compared(AtMost, ConstraintValue.Integer 1L))
                      RelationAtom(Within(ConstraintValue.Integer 1L, ConstraintValue.Integer 2L))
                      UniquenessAtom
                      MembershipAtom(Membership.Contains(ConstraintValue.Integer 1L))
                      NumberAtom(MultipleOf(ConstraintValue.Integer 5L))
                      NumberAtom Finite ]
            @>

        [<Fact>]
        let ``uniqueness reports the first duplicate as the actual value`` () =
            let tags: Constraint<string list> = Constraint.distinct

            test <@ violation tags [ "a"; "b"; "a" ] = Atomic(Expected(UniquenessAtom, Some(ConstraintValue.Text "a"))) @>

        [<Fact>]
        let ``the excluding membership rules stay interpreted rather than going opaque`` () =
            // The point of the pair: `notWith "..." (oneOf ...)` runs identically but describes as opaque, so
            // nothing downstream can read it. These build their own atoms and stay inspectable.
            let handle: Constraint<string> = Constraint.noneOf [ "admin"; "root" ]
            let tags: Constraint<string list> = Constraint.notContains "internal"

            test <@
                expression handle =
                    ConstraintExpression.Atom(
                        MembershipAtom(NoneOf [ ConstraintValue.Text "admin"; ConstraintValue.Text "root" ])
                    )
            @>

            test <@
                expression tags =
                    ConstraintExpression.Atom(MembershipAtom(Membership.NotContains(ConstraintValue.Text "internal")))
            @>

            test <@ Constraint.check handle "ada" = Ok() @>
            test <@ Constraint.check tags [ "public" ] = Ok() @>

            test <@
                violation handle "admin" =
                    Atomic(
                        Expected(
                            MembershipAtom(NoneOf [ ConstraintValue.Text "admin"; ConstraintValue.Text "root" ]),
                            Some(ConstraintValue.Text "admin")
                        )
                    )
            @>

            test <@ Violation.render (violation tags [ "internal" ]) = "expected the collection not to contain internal" @>

        [<Fact>]
        let ``a null collection fails notContains rather than vacuously satisfying it`` () =
            // Rule 3 of the null contract: a content rule never reads a missing reference as satisfied. Absence is
            // what `present` and `optional` speak about.
            let tags: Constraint<string array> = Constraint.notContains "internal"
            let missing: string array = null

            test <@ not (Constraint.test tags missing) @>
            test <@ violation tags missing = Atomic(Expected(MembershipAtom(Membership.NotContains(ConstraintValue.Text "internal")), None)) @>

        [<Fact>]
        let ``an excluding rule with a nonportable operand stays executable and reports honestly`` () =
            let reference: Constraint<Version> = Constraint.noneOf [ Version(1, 0) ]

            test <@ Constraint.test reference (Version(2, 0)) @>
            test <@ not (Constraint.test reference (Version(1, 0))) @>
            test <@ expression reference = ConstraintExpression.Opaque(OpaqueConstraint.UnsupportedOperand(UnsupportedOperation.Relation Equal)) @>

        [<Fact>]
        let ``the sign and count names build the same atoms their general forms build`` () =
            // These are spellings, not new primitives. If one ever built its own atom, inspection, lowering, and
            // generation would each need a second case for a rule the catalogue already covers.
            let named =
                [ expression (Constraint.positive: Constraint<int>)
                  expression (Constraint.nonNegative: Constraint<int>)
                  expression (Constraint.negative: Constraint<int>)
                  expression (Constraint.nonPositive: Constraint<int>)
                  expression (Constraint.single: Constraint<string list>)
                  expression (Constraint.atLeastOne: Constraint<string list>)
                  expression (Constraint.atMostOne: Constraint<string list>)
                  expression (Constraint.moreThanOne: Constraint<string list>) ]

            let general =
                [ expression (Constraint.greaterThan 0)
                  expression (Constraint.atLeast 0)
                  expression (Constraint.lessThan 0)
                  expression (Constraint.atMost 0)
                  expression (Constraint.length 1: Constraint<string list>)
                  expression (Constraint.minLength 1: Constraint<string list>)
                  expression (Constraint.maxLength 1: Constraint<string list>)
                  expression (Constraint.minLength 2: Constraint<string list>) ]

            test <@ named = general @>

        [<Fact>]
        let ``the sign names carry the zero of the value's own numeric type`` () =
            // GenericZero, not a boxed int: a decimal rule must describe its bound as a decimal so the operand
            // survives lowering with the precision the runtime comparison used.
            test <@
                expression (Constraint.positive: Constraint<decimal>) =
                    ConstraintExpression.Atom(RelationAtom(Compared(GreaterThan, ConstraintValue.Decimal 0M)))
            @>

            test <@ Constraint.check (Constraint.positive: Constraint<decimal>) 0.5M = Ok() @>
            test <@ Constraint.test (Constraint.nonNegative: Constraint<int64>) 0L @>
            test <@ not (Constraint.test (Constraint.negative: Constraint<float>) 0.0) @>

        [<Fact>]
        let ``bounds and prose are validated at construction`` () =
            // Assert.Throws rather than Unquote's `raises`: these constructors are inline SRTP values, and a
            // quotation would have to invoke the dispatcher dynamically.
            Assert.Throws<ArgumentOutOfRangeException>(fun () -> (Constraint.minLength -1: Constraint<string>) |> ignore)
            |> ignore

            Assert.Throws<ArgumentException>(fun () -> (Constraint.lengthBetween 4 2: Constraint<string>) |> ignore)
            |> ignore

            Assert.Throws<ArgumentException>(fun () -> Constraint.between 4 2 |> ignore) |> ignore
            Assert.Throws<ArgumentException>(fun () -> Constraint.custom "  " (fun (_: int) -> true) |> ignore) |> ignore
            Assert.Throws<ArgumentException>(fun () -> Constraint.notWith "" (Constraint.equalTo 1) |> ignore) |> ignore
            Assert.Throws<ArgumentException>(fun () -> Constraint.describe " " (Constraint.equalTo 1) |> ignore) |> ignore

    // ---------------------------------------------------------------------------------------------------
    // Composition
    // ---------------------------------------------------------------------------------------------------

    module Composition =
        [<Fact>]
        let ``all is the satisfied identity for an empty list`` () =
            let identity: Constraint<int> = Constraint.all []

            test <@ Constraint.test identity 1 @>
            test <@ Constraint.check identity 1 = Ok() @>
            test <@ expression identity = ConstraintExpression.All [] @>

        [<Fact>]
        let ``all evaluates in declaration order and accumulates every failure`` () =
            let mutable order = []

            let recording name predicate =
                Constraint.custom name (fun value ->
                    order <- name :: order
                    predicate value)

            let constraint' = Constraint.all [ recording "first" (fun _ -> false); recording "second" (fun _ -> false) ]
            let failure = violation constraint' 0

            test <@ List.rev order = [ "first"; "second" ] @>
            test <@ failure = All(Atomic(Described "first"), [ Atomic(Described "second") ]) @>

        [<Fact>]
        let ``any short-circuits on the first success and groups every rejected branch`` () =
            let mutable evaluated = 0

            let counting predicate =
                Constraint.custom "counted" (fun value ->
                    evaluated <- evaluated + 1
                    predicate value)

            let ttl = Constraint.any (Constraint.equalTo -1) [ counting (fun value -> value >= 1) ]

            test <@ Constraint.check ttl -1 = Ok() @>
            test <@ evaluated = 0 @>

            let failure = violation ttl 0

            test <@
                failure =
                    Any(
                        Atomic(Expected(RelationAtom(Compared(Equal, ConstraintValue.Integer -1L)), Some(ConstraintValue.Integer 0L))),
                        [ Atomic(Described "counted") ]
                    )
            @>

        [<Fact>]
        let ``a single failing child is returned directly rather than wrapped in a group`` () =
            let constraint' = Constraint.all [ Constraint.atLeast 0; Constraint.atMost 10 ]

            test <@ violation constraint' 42 = Atomic(Expected(RelationAtom(Compared(AtMost, ConstraintValue.Integer 10L)), Some(ConstraintValue.Integer 42L))) @>

        [<Fact>]
        let ``notWith is opaque, retains its inner expression, and reports the supplied prose`` () =
            let reserved = Constraint.oneOf [ "admin"; "root" ]
            let constraint' = Constraint.notWith "must not be a reserved name" reserved

            test <@ Constraint.test constraint' "ada" @>
            test <@ violation constraint' "admin" = Atomic(Described "must not be a reserved name") @>

            match expression constraint' with
            | ConstraintExpression.Opaque(OpaqueConstraint.RuntimeNegation(description, inner)) ->
                test <@ description = "must not be a reserved name" @>
                test <@ inner = Constraint.inspect reserved @>
            | other -> failwithf "Expected a runtime negation, but was %A." other

        [<Fact>]
        let ``contramap is opaque but retains the inner expression beneath the boundary`` () =
            let inner: Constraint<string> = Constraint.present
            let constraint' = inner |> Constraint.contramap (fun (value: {| Name: string |}) -> value.Name)

            test <@ Constraint.test constraint' {| Name = "Ada" |} @>
            test <@ not (Constraint.test constraint' {| Name = " " |}) @>
            test <@ expression constraint' = ConstraintExpression.Opaque(OpaqueConstraint.RuntimeProjection(Constraint.inspect inner)) @>

        [<Fact>]
        let ``an opaque child never erases its portable siblings`` () =
            let constraint' =
                Constraint.all [ Constraint.atLeast 0; Constraint.custom "must be even" (fun value -> value % 2 = 0) ]

            match expression constraint' with
            | ConstraintExpression.All [ first; second ] ->
                test <@ first.Expression = ConstraintExpression.Atom(RelationAtom(Compared(AtLeast, ConstraintValue.Integer 0L))) @>
                test <@ second.Expression = ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate "must be even") @>
            | other -> failwithf "Expected a conjunction, but was %A." other

        [<Fact>]
        let ``describe attaches prose without changing the failure or the logical form`` () =
            let described = Constraint.between 0 10 |> Constraint.describe "Retries before the call is abandoned."
            let description = Constraint.inspect described

            test <@ description.Description = Some "Retries before the call is abandoned." @>
            test <@ description.Expression = expression (Constraint.between 0 10) @>
            test <@ violation described 42 = violation (Constraint.between 0 10) 42 @>

        [<Fact>]
        let ``custom failures retain their authored prose and customWith may report a typed reason`` () =
            let isbn = Constraint.custom "must be a valid ISBN" (fun (value: string) -> value.Length = 13)

            test <@ violation isbn "short" = Atomic(Described "must be a valid ISBN") @>

            let currency =
                Constraint.customWith "must be a supported currency" (fun code ->
                    if code = "AUD" then
                        Ok()
                    else
                        Error(Atomic(Expected(MembershipAtom(OneOf [ ConstraintValue.Text "AUD" ]), ConstraintValue.tryCreate code))))

            test <@
                violation currency "NZD" =
                    Atomic(Expected(MembershipAtom(OneOf [ ConstraintValue.Text "AUD" ]), Some(ConstraintValue.Text "NZD")))
            @>

            // The enclosing description stays opaque, so a typed reason makes no false portable claim.
            test <@ expression currency = ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate "must be a supported currency") @>

    // ---------------------------------------------------------------------------------------------------
    // Portable values
    // ---------------------------------------------------------------------------------------------------

    module PortableValues =
        [<Fact>]
        let ``conversion is total and never throws for any float`` () =
            let floats = [ nan; infinity; -infinity; 1e30; -0.0; 0.0 ]

            test <@ floats |> List.forall (fun value -> (ConstraintValue.tryCreate value).IsSome) @>
            test <@ ConstraintValue.tryCreate (fun () -> ()) = None @>

        [<Fact>]
        let ``floats are not converted through decimal`` () =
            test <@ ConstraintValue.tryCreate 1e30 = Some(ConstraintValue.ofFloat 1e30) @>
            test <@ Constraint.test (Constraint.lessThan infinity) 1.0 @>

        [<Fact>]
        let ``a portable float keeps structural self-equality for NaN and signed zero`` () =
            test <@ ConstraintValue.ofFloat nan = ConstraintValue.ofFloat nan @>
            test <@ ConstraintValue.ofFloat 0.0 <> ConstraintValue.ofFloat -0.0 @>
            test <@ ConstraintValue.ofFloat32 nanf = ConstraintValue.ofFloat32 nanf @>
            test <@ ConstraintValue.ofFloat32 0.0f <> ConstraintValue.ofFloat32 -0.0f @>

        [<Fact>]
        let ``semantic sorts keep their own case rather than becoming text`` () =
            let instant = DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero)
            let local = instant.DateTime
            let identifier = Guid.NewGuid()
            let span = TimeSpan.FromMinutes 1.0

            test <@ ConstraintValue.ofOperand instant = Some(ConstraintValue.DateTimeOffset instant) @>
            test <@ ConstraintValue.ofOperand local = Some(ConstraintValue.DateTime local) @>
            test <@ ConstraintValue.ofOperand identifier = Some(ConstraintValue.Guid identifier) @>
            test <@ ConstraintValue.ofOperand span = Some(ConstraintValue.TimeSpan span) @>

        [<Fact>]
        let ``an operand keeps its semantic sort on every platform`` () =
            // Fable erases a Guid to a plain string and a TimeSpan to a number, so a boxed type test labels them
            // `Text` and `Integer` there while .NET labels them correctly. Projection resolves on the static type
            // at the call site instead, so one constraint cannot mean two things. The Fable JS surface check
            // asserts the same expectations against the compiled JavaScript.
            let identifier = Guid.NewGuid()
            let span = TimeSpan.FromMinutes 1.0

            test <@ expression (Constraint.equalTo identifier) = ConstraintExpression.Atom(RelationAtom(Compared(Equal, ConstraintValue.Guid identifier))) @>
            test <@ expression (Constraint.atLeast span) = ConstraintExpression.Atom(RelationAtom(Compared(AtLeast, ConstraintValue.TimeSpan span))) @>
            test <@ expression (Constraint.oneOf [ identifier ]) = ConstraintExpression.Atom(MembershipAtom(OneOf [ ConstraintValue.Guid identifier ])) @>

            // The projection still reports honestly for a type no representation covers.
            test <@ expression (Constraint.equalTo (Version(1, 0))) = ConstraintExpression.Opaque(OpaqueConstraint.UnsupportedOperand(UnsupportedOperation.Relation Equal)) @>

        [<Fact>]
        let ``integer boundaries, null, and nested lists round-trip through the portable set`` () =
            test <@ ConstraintValue.tryCreate Int64.MaxValue = Some(ConstraintValue.Integer Int64.MaxValue) @>
            test <@ ConstraintValue.tryCreate Int64.MinValue = Some(ConstraintValue.Integer Int64.MinValue) @>
            test <@ ConstraintValue.tryCreate (null: string) = Some ConstraintValue.Null @>
            test <@ ConstraintValue.tryCreate [ [ 1 ] ] = Some(ConstraintValue.List [ ConstraintValue.List [ ConstraintValue.Integer 1L ] ]) @>

        [<Fact>]
        let ``an unsupported operand executes normally, inspects as opaque, and exposes no boxed value`` () =
            // Version compares and hashes, so the rule executes; it has no portable representation, so the
            // operand is never exposed and the atom declines to name it.
            let expected = Version(1, 0)
            let unsupported = Constraint.equalTo expected

            test <@ Constraint.test unsupported expected @>
            test <@ not (Constraint.test unsupported (Version(2, 0))) @>
            test <@ expression unsupported = ConstraintExpression.Opaque(OpaqueConstraint.UnsupportedOperand(UnsupportedOperation.Relation Equal)) @>
            test <@ violation unsupported (Version(2, 0)) = Atomic(UnsupportedOperand(UnsupportedOperation.Relation Equal)) @>

        [<Fact>]
        let ``an unsupported operand keeps the surrounding structure inspectable`` () =
            let constraint' =
                Constraint.all [ Constraint.equalTo (Version(1, 0)); Constraint.atLeast (Version(1, 0)) ]

            match expression constraint' with
            | ConstraintExpression.All [ first; second ] ->
                test <@ ConstraintDescription.isOpaque first && ConstraintDescription.isOpaque second @>
            | other -> failwithf "Expected a conjunction, but was %A." other

    // ---------------------------------------------------------------------------------------------------
    // Violations
    // ---------------------------------------------------------------------------------------------------

    module Violations =
        [<Fact>]
        let ``failure families that used to collapse together are now distinct`` () =
            let families =
                [ violation (Constraint.equalTo 1) 2
                  violation (Constraint.notEqualTo 1) 1
                  violation (Constraint.oneOf [ "a" ]) "b"
                  violation (Constraint.contains 1: Constraint<int list>) []
                  violation (Constraint.multipleOf 5) 3
                  violation Constraint.finite nan
                  violation (Constraint.distinct: Constraint<int list>) [ 1; 1 ] ]

            test <@ families |> List.distinct |> List.length = List.length families @>

        [<Fact>]
        let ``a violation is plain comparable data with no closures or descriptions reachable from it`` () =
            let constraint' = Constraint.all [ Constraint.atLeast 0; Constraint.atMost 10 ]

            let retained =
                // The constraint deliberately goes out of scope: a violation must stay comparable without it.
                violation constraint' 42

            test <@ retained = retained @>
            test <@ retained = violation (Constraint.all [ Constraint.atLeast 0; Constraint.atMost 10 ]) 42 @>
            test <@ hash retained = hash retained @>

        [<Fact>]
        let ``a whole violation maps once into a domain error`` () =
            let name: Constraint<string> = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 4 ]

            let rejected =
                "" |> Constraint.guard name |> Result.mapError (fun violation -> $"name rejected: {Violation.render violation}")

            test <@ rejected = Error "name rejected: value must be present; expected a size between 2 and 4, but was 0" @>

        [<Fact>]
        let ``projections and traversals work over nested groups`` () =
            let inner = All(Atomic(Described "a"), [ Atomic(Described "b") ])
            let tree = Any(inner, [ Atomic(Described "c") ])

            test <@ Violation.children tree = [ inner; Atomic(Described "c") ] @>
            test <@ Violation.flatten tree = [ Described "a"; Described "b"; Described "c" ] @>
            test <@ Violation.tryDescription (Atomic(Described "a")) = Some "a" @>
            test <@ Violation.tryDescription tree = None @>

        [<Fact>]
        let ``a leaf carries the failing constraint's own identity rather than a parsed code`` () =
            let failure = violation (Constraint.minLength 3: Constraint<string>) "ab"

            test <@ Violation.tryExpectation failure = Some(CardinalityAtom(Cardinality.Minimum 3)) @>
            test <@ Violation.tryActual failure = Some(ConstraintValue.Integer 2L) @>

        [<Fact>]
        let ``rendering keeps conjunction and alternative groups distinct`` () =
            let conjunction = All(Atomic(Described "a"), [ Atomic(Described "b") ])
            let alternatives = Any(Atomic(Described "a"), [ Atomic(Described "b") ])

            test <@ Violation.render conjunction = "a; b" @>
            test <@ Violation.render alternatives = "a, or b" @>

        [<Fact>]
        let ``message keys cover every reason and expectation case`` () =
            let atoms =
                [ PresenceAtom Present
                  PresenceAtom Blank
                  CardinalityAtom(Exact 1)
                  CardinalityAtom(Cardinality.Minimum 1)
                  CardinalityAtom(Cardinality.Maximum 1)
                  CardinalityAtom(Cardinality.Between(1, 2))
                  RelationAtom(Compared(Equal, ConstraintValue.Integer 1L))
                  RelationAtom(Compared(NotEqual, ConstraintValue.Integer 1L))
                  RelationAtom(Compared(GreaterThan, ConstraintValue.Integer 1L))
                  RelationAtom(Compared(LessThan, ConstraintValue.Integer 1L))
                  RelationAtom(Compared(AtLeast, ConstraintValue.Integer 1L))
                  RelationAtom(Compared(AtMost, ConstraintValue.Integer 1L))
                  RelationAtom(Within(ConstraintValue.Integer 1L, ConstraintValue.Integer 2L))
                  MembershipAtom(OneOf [])
                  MembershipAtom(NoneOf [])
                  MembershipAtom(Membership.Contains(ConstraintValue.Integer 1L))
                  MembershipAtom(Membership.NotContains(ConstraintValue.Integer 1L))
                  UniquenessAtom
                  FormatAtom Email
                  FormatAtom Trimmed
                  FormatAtom Numeric
                  FormatAtom Alphanumeric
                  FormatAtom(Pattern "^a$")
                  NumberAtom(MultipleOf(ConstraintValue.Integer 1L))
                  NumberAtom Finite ]

            let keys = atoms |> List.map ConstraintAtom.key

            test <@ keys |> List.distinct |> List.length = List.length keys @>
            test <@ keys |> List.forall (fun key -> key.StartsWith "constraint.") @>
            test <@ atoms |> List.forall (ConstraintAtom.render >> String.IsNullOrWhiteSpace >> not) @>

        [<Fact>]
        let ``unsupported-operand keys compose from the operation and its operator`` () =
            test <@ UnsupportedOperation.key (UnsupportedOperation.Relation AtLeast) = "constraint.unsupportedOperand.relation.atLeast" @>
            test <@ UnsupportedOperation.key UnsupportedOperation.MultipleOf = "constraint.unsupportedOperand.multipleOf" @>

        [<Fact>]
        let ``toMessageTree preserves grouping and separates localizable keys from authored prose`` () =
            let tree =
                Violation.toMessageTree (All(Atomic(Expected(PresenceAtom Present, None)), [ Atomic(Described "authored") ]))

            match tree with
            | MessageTree.All(MessageTree.Leaf(MessageLeaf.Localized descriptor), [ MessageTree.Leaf(MessageLeaf.Verbatim prose) ]) ->
                test <@ descriptor.Key = "constraint.presence.present" @>
                test <@ prose = "authored" @>
            | other -> failwithf "Expected a conjunction of one localized and one verbatim leaf, but was %A." other

        [<Fact>]
        let ``a message descriptor carries the expectation operands and the actual value`` () =
            let failure = violation (Constraint.between 0 10) 42

            match Violation.toMessageTree failure with
            | MessageTree.Leaf(MessageLeaf.Localized descriptor) ->
                test <@ descriptor.Key = "constraint.relation.within" @>
                test <@ descriptor.Arguments["minimum"] = ConstraintValue.Integer 0L @>
                test <@ descriptor.Arguments["maximum"] = ConstraintValue.Integer 10L @>
                test <@ descriptor.Arguments["actual"] = ConstraintValue.Integer 42L @>
            | other -> failwithf "Expected one localized leaf, but was %A." other

        [<Fact>]
        let ``conjoin and alternatives normalize empty and unary groups away`` () =
            let single = Atomic(Described "a")

            test <@ Violation.conjoin [] = None @>
            test <@ Violation.conjoin [ single ] = Some single @>
            test <@ Violation.conjoin [ single; single ] = Some(All(single, [ single ])) @>
            test <@ Violation.alternatives [ single ] = Some single @>
            test <@ Violation.alternatives [ single; single ] = Some(Any(single, [ single ])) @>

    // ---------------------------------------------------------------------------------------------------
    // DSL
    // ---------------------------------------------------------------------------------------------------

    module Dsl =
        // `test` is one of the names the DSL deliberately exports, so it shadows Unquote's assertion here. That
        // is exactly the collision the DSL documents; the alias keeps both available.
        let inline assertThat (assertion: Quotations.Expr<bool>) = test assertion

        open Axial.Constraint.ConstraintDSL

        [<Fact>]
        let ``the DSL yields the same values the qualified names return`` () =
            let dslName: Constraint<string> = Constraint.all [ present; lengthBetween 2 40 ]
            let qualifiedName: Constraint<string> = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

            assertThat <@ Constraint.inspect dslName = Constraint.inspect qualifiedName @>
            assertThat <@ Constraint.test dslName "Ada" = Constraint.test qualifiedName "Ada" @>

        [<Fact>]
        let ``the sign and count names are reachable unqualified`` () =
            // Both sides are resolved outside the quotation: these are inline SRTP values, so a quotation would
            // have to invoke the dispatcher dynamically.
            let viaDsl =
                [ Constraint.inspect (positive: Constraint<int>)
                  Constraint.inspect (nonNegative: Constraint<decimal>)
                  Constraint.inspect (negative: Constraint<int>)
                  Constraint.inspect (nonPositive: Constraint<int>)
                  Constraint.inspect (single: Constraint<string list>)
                  Constraint.inspect (atLeastOne: Constraint<string list>)
                  Constraint.inspect (atMostOne: Constraint<string list>)
                  Constraint.inspect (moreThanOne: Constraint<string list>) ]

            let viaQualified =
                [ Constraint.inspect (Constraint.positive: Constraint<int>)
                  Constraint.inspect (Constraint.nonNegative: Constraint<decimal>)
                  Constraint.inspect (Constraint.negative: Constraint<int>)
                  Constraint.inspect (Constraint.nonPositive: Constraint<int>)
                  Constraint.inspect (Constraint.single: Constraint<string list>)
                  Constraint.inspect (Constraint.atLeastOne: Constraint<string list>)
                  Constraint.inspect (Constraint.atMostOne: Constraint<string list>)
                  Constraint.inspect (Constraint.moreThanOne: Constraint<string list>) ]

            assertThat <@ viaDsl = viaQualified @>

        [<Fact>]
        let ``guard, orError, and mapError finish a constraint pipeline with the application's error`` () =
            let age: Constraint<int> = atLeast 13

            assertThat <@ 16 |> guard age |> orError "too young" = Ok 16 @>
            assertThat <@ 11 |> guard age |> orError "too young" = Error "too young" @>
            assertThat <@ 11 |> guard age |> mapError Violation.render = Error "expected a value at least 13, but was 11" @>
