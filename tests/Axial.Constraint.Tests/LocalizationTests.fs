namespace Axial.Tests

open System
open System.Globalization
open Axial.Constraint
open Swensen.Unquote
open Xunit

/// <summary>
/// The rendering edge: contextual fallback, plural precedence, interpolation, value formatting, group joining,
/// and the composition contract between <c>message</c> and <c>fullMessage</c>.
/// </summary>
module LocalizationTests =

    let private lookupOf pairs =
        let table = Map.ofList pairs
        Renderer.ofLookup table.TryFind

    let private present () = Atomic(Expected(PresenceAtom Present, None))

    let private atLeast expected actual =
        Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Integer expected)), Some(ConstraintValue.Integer actual)))

    let private specOf key plural arguments =
        MessageDescriptor.Advanced.create key arguments
        |> MessageFormatSpec.Advanced.create "neutral {count}" plural

    // ---------------------------------------------------------------------------------------------------
    // Relative keys and segment encoding
    // ---------------------------------------------------------------------------------------------------

    module Keys =
        [<Fact>]
        let ``a malformed key fails at construction and cannot reach rendering`` () =
            test <@ MessageDescriptor.Advanced.tryCreate "" Map.empty = Error MessageKeyError.EmptyKey @>

            test <@
                MessageDescriptor.Advanced.tryCreate "books..isbn" Map.empty
                    = Error(MessageKeyError.EmptySegment 1)
            @>

            raises<ArgumentException> <@ MessageDescriptor.Advanced.create "books..isbn" Map.empty @>
            raises<ArgumentException> <@ Constraint.customLocalized "" "prose" (fun (_: string) -> true) @>

        [<Fact>]
        let ``a key round-trips to its canonical unencoded text`` () =
            let descriptor = MessageDescriptor.Advanced.create "books.isbn.invalid" Map.empty

            test <@ MessageDescriptor.key descriptor = "books.isbn.invalid" @>
            test <@ MessageDescriptor.segments descriptor = [ "books"; "isbn"; "invalid" ] @>

        [<Fact>]
        let ``equivalent descriptors and the violations containing them compare equal`` () =
            let arguments = Map [ "expectedLength", ConstraintValue.Integer 13L ]
            let left = MessageDescriptor.Advanced.create "books.isbn.invalid" arguments
            let right = MessageDescriptor.Advanced.ofSegments [ "books"; "isbn"; "invalid" ] arguments

            test <@ left = right @>
            test <@ Atomic(Described("prose", Some left)) = Atomic(Described("prose", Some right)) @>

        [<Fact>]
        let ``a dot inside a segment cannot collide with a segment boundary`` () =
            // One context named "address.postcode" and two contexts named "address" then "postcode" are
            // different scopes; encoding the dot is what keeps a translator's entries from silently merging.
            let joined = Renderer.english |> Renderer.context "address.postcode"

            let nested =
                Renderer.english |> Renderer.context "address" |> Renderer.context "postcode"

            let spec = specOf "constraint.presence.present" None Map.empty

            test <@ Renderer.Advanced.lookupCandidates joined spec <> Renderer.Advanced.lookupCandidates nested spec @>

            test <@
                Renderer.Advanced.lookupCandidates joined spec |> List.head
                    = "address%2Epostcode.constraint.presence.present"
            @>

        [<Fact>]
        let ``a literal percent stays distinct from an encoded reserved character`` () =
            let literal = Renderer.english |> Renderer.context "%2E"
            let dot = Renderer.english |> Renderer.context "."
            let spec = specOf "constraint.presence.present" None Map.empty

            test <@ Renderer.Advanced.lookupCandidates literal spec |> List.head = "%252E.constraint.presence.present" @>
            test <@ Renderer.Advanced.lookupCandidates dot spec |> List.head = "%2E.constraint.presence.present" @>

        [<Fact>]
        let ``a plural operand must name an argument the descriptor carries`` () =
            let descriptor = MessageDescriptor.Advanced.create "schema.items" Map.empty

            test <@
                MessageFormatSpec.Advanced.tryCreate "neutral" (Some "count") descriptor
                    = Error(MessageFormatSpecError.UnknownPluralArgument "count")
            @>

            raises<ArgumentException> <@ MessageFormatSpec.Advanced.create "neutral" (Some "count") descriptor @>

    // ---------------------------------------------------------------------------------------------------
    // Context, attributes, and fallback order
    // ---------------------------------------------------------------------------------------------------

    module Scope =
        [<Fact>]
        let ``fallback removes rightmost specificity without ever truncating the identity`` () =
            let renderer =
                Renderer.english
                |> Renderer.context "signup"
                |> Renderer.Advanced.attributePath [ "address"; "postcode" ]

            let candidates =
                Renderer.Advanced.lookupCandidates renderer (specOf "constraint.presence.present" None Map.empty)

            test <@
                candidates =
                    [ "signup.address.postcode.constraint.presence.present"
                      "signup.address.constraint.presence.present"
                      "signup.constraint.presence.present"
                      "constraint.presence.present" ]
            @>

        [<Fact>]
        let ``a context segment can never become an attribute noun`` () =
            // "signup" names the document, not the field. Borrowing it as a noun would produce "Signup must be
            // present" for a whole-model failure.
            let renderer = lookupOf [] |> Renderer.context "signup"

            test <@ Renderer.attributeName renderer = "value" @>
            test <@ Violation.fullMessage renderer (present ()) = "value must be present" @>

        [<Fact>]
        let ``replacing an attribute cannot retain the previous sibling`` () =
            let form = Renderer.english |> Renderer.context "signup"
            let name = form |> Renderer.attribute "name"
            let email = name |> Renderer.attribute "email"

            test <@ Renderer.Advanced.attributeCandidates email = [ "attribute.signup.email"; "attribute.email" ] @>

        [<Fact>]
        let ``unscoped clears both roles`` () =
            let renderer =
                Renderer.english |> Renderer.context "signup" |> Renderer.attribute "name" |> Renderer.unscoped

            test <@
                Renderer.Advanced.lookupCandidates renderer (specOf "constraint.presence.present" None Map.empty)
                    = [ "constraint.presence.present" ]
            @>

        [<Fact>]
        let ``attribute nouns resolve from most to least specific`` () =
            let renderer =
                lookupOf [ "attribute.address.postcode", "Postal code" ]
                |> Renderer.context "signup"
                |> Renderer.Advanced.attributePath [ "address"; "postcode" ]

            test <@ Renderer.attributeName renderer = "Postal code" @>

        [<Fact>]
        let ``an unnamed attribute humanizes only its final raw segment`` () =
            let name segment =
                Renderer.english |> Renderer.attribute segment |> Renderer.attributeName

            test <@ name "postcode" = "Postcode" @>
            test <@ name "firstName" = "First name" @>
            test <@ name "postcodeID" = "Postcode ID" @>
            test <@ name "billing_address" = "Billing address" @>
            test <@ name "shipping-address" = "Shipping address" @>
            test <@ name "customer_id" = "Customer id" @>

            let nested =
                Renderer.english |> Renderer.Advanced.attributePath [ "address"; "postcode" ]

            test <@ Renderer.attributeName nested = "Postcode" @>

        [<Fact>]
        let ``a translated string is returned exactly as authored`` () =
            // No recasing, trimming, or normalizing. A locale that wants a lowercase noun mid-sentence, or
            // leading whitespace, gets exactly what its translator wrote.
            let renderer =
                lookupOf [ "attribute.name", "  le nom  " ] |> Renderer.attribute "name"

            test <@ Renderer.attributeName renderer = "  le nom  " @>

    // ---------------------------------------------------------------------------------------------------
    // Plural selection
    // ---------------------------------------------------------------------------------------------------

    module Plural =
        let private minimumSpec value =
            MessageDescriptor.Advanced.create
                "constraint.cardinality.minimum"
                (Map [ "minimum", ConstraintValue.Integer value ])
            |> MessageFormatSpec.Advanced.create "must have at least {minimum}" (Some "minimum")

        [<Fact>]
        let ``ordinary lookup selects one for exactly one and other otherwise`` () =
            let renderer =
                lookupOf
                    [ "constraint.cardinality.minimum.one", "must have at least {minimum} item"
                      "constraint.cardinality.minimum.other", "must have at least {minimum} items" ]

            test <@ renderer |> Renderer.Advanced.format (minimumSpec 1L) = "must have at least 1 item" @>
            test <@ renderer |> Renderer.Advanced.format (minimumSpec 3L) = "must have at least 3 items" @>

        [<Fact>]
        let ``a bare field entry beats a pluralized model entry`` () =
            // Precedence is by contextual level first, plural second. A field-specific override is the more
            // deliberate statement, so it wins even though it is not pluralized.
            let renderer =
                lookupOf
                    [ "signup.tags.constraint.cardinality.minimum", "pick some tags"
                      "signup.constraint.cardinality.minimum.other", "must have at least {minimum} entries" ]
                |> Renderer.context "signup"
                |> Renderer.attribute "tags"

            test <@ renderer |> Renderer.Advanced.format (minimumSpec 3L) = "pick some tags" @>

        [<Fact>]
        let ``an entry with no declared operand asks for no plural key`` () =
            let renderer = Renderer.english |> Renderer.context "signup"
            let spec = specOf "constraint.presence.present" None Map.empty

            test <@
                Renderer.Advanced.lookupCandidates renderer spec
                    = [ "signup.constraint.presence.present"; "constraint.presence.present" ]
            @>

        [<Fact>]
        let ``a custom localized constraint declares no plural operand`` () =
            let isbn =
                Constraint.customLocalizedWith
                    "books.isbn.invalid"
                    "must be a valid ISBN"
                    (Map [ "expectedLength", ConstraintValue.Integer 1L ])
                    (fun (_: string) -> false)

            let renderer = lookupOf [ "books.isbn.invalid.one", "singular" ]

            match Constraint.check isbn "x" with
            | Error failure -> test <@ Violation.message renderer failure = "must be a valid ISBN" @>
            | Ok() -> failwith "Expected the constraint to reject the value."

    // ---------------------------------------------------------------------------------------------------
    // Advanced resolvers
    // ---------------------------------------------------------------------------------------------------

    module Resolvers =
        [<Fact>]
        let ``a resolver receives one encoded base-key request per contextual level`` () =
            let seen = ResizeArray<MessageRequest>()

            let renderer =
                Renderer.Advanced.ofResolver (fun request ->
                    seen.Add request
                    None)
                |> Renderer.context "sign.up"
                |> Renderer.attribute "name"

            renderer
            |> Renderer.Advanced.format (specOf "constraint.presence.present" None Map.empty)
            |> ignore

            test <@
                seen |> Seq.map _.BaseKey |> List.ofSeq =
                    [ "sign%2Eup.name.constraint.presence.present"
                      "sign%2Eup.constraint.presence.present"
                      "constraint.presence.present" ]
            @>

        [<Fact>]
        let ``a rendered resolution is never interpolated again`` () =
            // An ICU adapter has already substituted; a second pass would eat literal braces the translator
            // deliberately kept.
            let renderer =
                Renderer.Advanced.ofResolver (fun _ -> Some(MessageResolution.Rendered "au moins {expected} — {{literal}}"))

            test <@ Violation.message renderer (atLeast 13L 11L) = "au moins {expected} — {{literal}}" @>

        [<Fact>]
        let ``a rendered leaf still composes into actual, group, and full message`` () =
            let renderer =
                Renderer.Advanced.ofResolver (fun request ->
                    if request.BaseKey = "constraint.presence.present" then
                        Some(MessageResolution.Rendered "PRESENT")
                    else
                        None)
                |> Renderer.attribute "name"

            test <@ Violation.fullMessage renderer (present ()) = "Name PRESENT" @>

        [<Fact>]
        let ``messageRequests reports the owning catalogue's plural operand`` () =
            let renderer = Renderer.english |> Renderer.context "signup"

            let spec =
                MessageDescriptor.Advanced.ofSegments
                    [ "schema"; "items" ]
                    (Map [ "count", ConstraintValue.Integer 2L ])
                |> MessageFormatSpec.Advanced.create "must have {count} items" (Some "count")

            let requests = Renderer.Advanced.messageRequests renderer spec

            test <@ requests |> List.map _.BaseKey = [ "signup.schema.items"; "schema.items" ] @>
            test <@ requests |> List.forall (fun request -> request.PluralArgument = Some "count") @>

            // The same generic path a foreign catalogue relies on, with no knowledge of it in Axial.Constraint.
            let translated =
                lookupOf [ "schema.items.other", "doit contenir {count} éléments" ]
                |> Renderer.Advanced.format spec

            test <@ translated = "doit contenir 2 éléments" @>

    // ---------------------------------------------------------------------------------------------------
    // Templates and value formatting
    // ---------------------------------------------------------------------------------------------------

    module Templates =
        [<Fact>]
        let ``doubled braces produce literal braces`` () =
            let renderer = lookupOf [ "constraint.presence.present", "{{required}}" ]
            test <@ Violation.message renderer (present ()) = "{required}" @>

        [<Fact>]
        let ``an unknown placeholder name stays literal`` () =
            let renderer = lookupOf [ "constraint.presence.present", "needs {nothing}" ]
            test <@ Violation.message renderer (present ()) = "needs {nothing}" @>

        [<Fact>]
        let ``a malformed template falls through to the next candidate without throwing`` () =
            let renderer =
                lookupOf
                    [ "signup.constraint.relation.atLeast", "au moins {expected"
                      "constraint.relation.atLeast", "at least {expected}" ]
                |> Renderer.context "signup"

            test <@ Violation.message renderer (atLeast 13L 11L) = "at least 13, but was 11" @>

        [<Fact>]
        let ``a malformed template at every level reaches the generated neutral English`` () =
            let renderer = lookupOf [ "constraint.presence.present", "}" ]
            test <@ Violation.message renderer (present ()) = "must be present" @>

        [<Fact>]
        let ``an exception from an application callback propagates`` () =
            // A resource miss is data; a throwing callback is a defect, and swallowing it would hide the bug in
            // whichever locale nobody exercises.
            let renderer = Renderer.ofLookup (fun _ -> failwith "lookup is broken")

            raises<Exception> <@ Violation.message renderer (present ()) @>

        [<Fact>]
        let ``an unknown format suffix falls back to ordinary formatting`` () =
            let renderer = lookupOf [ "constraint.relation.atLeast", "at least {expected:zzz}" ]
            test <@ Violation.message renderer (atLeast 13L 11L) = "at least 13, but was 11" @>

        [<Fact>]
        let ``withValues replaces every operand rendering`` () =
            let renderer =
                lookupOf [ "constraint.relation.atLeast", "at least {expected:N0}" ]
                |> Renderer.withValues (fun _ -> "<value>")

            test <@ Violation.message renderer (atLeast 13L 11L) = "at least <value>, but was <value>" @>

        [<Fact>]
        let ``the advanced formatter receives the placeholder's suffix`` () =
            let seen = ResizeArray<string option>()

            let renderer =
                lookupOf [ "constraint.relation.atLeast", "at least {expected:N0}" ]
                |> Renderer.Advanced.withValueFormatting (fun request ->
                    seen.Add request.Format
                    ConstraintValue.render request.Value)

            Violation.message renderer (atLeast 13L 11L) |> ignore

            test <@ seen |> List.ofSeq = [ Some "N0"; None ] @>

    // ---------------------------------------------------------------------------------------------------
    // Lists and groups
    // ---------------------------------------------------------------------------------------------------

    module Joining =
        let private described text = Atomic(Described(text, None))

        let private oneOf values =
            Atomic(Expected(MembershipAtom(OneOf(values |> List.map ConstraintValue.Text)), None))

        [<Fact>]
        let ``list joining is deterministic from zero items upwards`` () =
            let render values = Violation.message Renderer.english (oneOf values)

            test <@ render [] = "must be one of " @>
            test <@ render [ "a" ] = "must be one of a" @>
            test <@ render [ "a"; "b" ] = "must be one of a and b" @>
            test <@ render [ "a"; "b"; "c" ] = "must be one of a, b and c" @>
            test <@ render [ "a"; "b"; "c"; "d" ] = "must be one of a, b, c and d" @>

        [<Fact>]
        let ``groups join through their own contextual patterns`` () =
            let conjunction = All(described "a", [ described "b"; described "c" ])
            let alternatives = Any(described "a", [ described "b"; described "c" ])

            test <@ Violation.message Renderer.english conjunction = "a, b and c" @>
            test <@ Violation.message Renderer.english alternatives = "a, b or c" @>

        [<Fact>]
        let ``a publicly constructed unary group renders its child with no pattern lookup`` () =
            let renderer = lookupOf [ "constraint.group.all.pair", "SHOULD NOT APPEAR" ]

            test <@ Violation.message renderer (All(described "only", [])) = "only" @>

        [<Fact>]
        let ``a translation may reorder a joining pattern`` () =
            let renderer = lookupOf [ "constraint.group.any.pair", "{second} ou bien {first}" ]

            test <@ Violation.message renderer (Any(described "a", [ described "b" ])) = "b ou bien a" @>

        [<Fact>]
        let ``the attribute noun is composed once around a whole group`` () =
            let group = All(described "a", [ described "b"; described "c" ])
            let renderer = Renderer.english |> Renderer.attribute "name"

            test <@ Violation.fullMessage renderer group = "Name a, b and c" @>

    // ---------------------------------------------------------------------------------------------------
    // Composition and compatibility
    // ---------------------------------------------------------------------------------------------------

    module Composition =
        [<Fact>]
        let ``render, message, and fullMessage match their documented contract`` () =
            let opaque = Atomic(Described("must be a valid ISBN", None))
            let group = All(present (), [ atLeast 13L 11L ])

            test <@ Violation.render (present ()) = "value must be present" @>
            test <@ Violation.message Renderer.english (present ()) = "must be present" @>
            test <@ Violation.fullMessage Renderer.english (present ()) = "value must be present" @>

            test <@ Violation.render (atLeast 13L 11L) = "expected a value at least 13, but was 11" @>
            test <@ Violation.message Renderer.english (atLeast 13L 11L) = "must be at least 13, but was 11" @>

            test <@
                Violation.fullMessage Renderer.english (atLeast 13L 11L) = "value must be at least 13, but was 11"
            @>

            test <@ Violation.render opaque = "must be a valid ISBN" @>
            test <@ Violation.message Renderer.english opaque = "must be a valid ISBN" @>
            test <@ Violation.fullMessage Renderer.english opaque = "value must be a valid ISBN" @>

            test <@ Violation.render group = "value must be present; expected a value at least 13, but was 11" @>

            test <@
                Violation.message Renderer.english group = "must be present and must be at least 13, but was 11"
            @>

        [<Fact>]
        let ``the actual clause is a separate entry a locale may reorder`` () =
            let renderer = lookupOf [ "constraint.actual", "reçu {actual} au lieu de « {message} »" ]

            test <@ Violation.message renderer (atLeast 13L 11L) = "reçu 11 au lieu de « must be at least 13 »" @>

        [<Fact>]
        let ``the noun entry is a separate entry a locale may reorder`` () =
            let renderer =
                lookupOf [ "constraint.fullMessage", "{message} — {attribute}" ]
                |> Renderer.attribute "name"

            test <@ Violation.fullMessage renderer (present ()) = "must be present — Name" @>

        [<Fact>]
        let ``an authored key wins over its prose, and the prose is the fallback`` () =
            let isbn =
                Constraint.customLocalized "books.isbn.invalid" "must be a valid ISBN" (fun (_: string) -> false)

            let failure =
                match Constraint.check isbn "x" with
                | Error failure -> failure
                | Ok() -> failwith "Expected the constraint to reject the value."

            test <@ Violation.message Renderer.english failure = "must be a valid ISBN" @>

            let translated =
                lookupOf [ "signup.books.isbn.invalid", "ISBN invalide" ]
                |> Renderer.context "signup"

            test <@ Violation.message translated failure = "ISBN invalide" @>

        [<Fact>]
        let ``custom key lookup narrows by context but never by key namespace`` () =
            let isbn =
                Constraint.customLocalized "books.isbn.invalid" "must be a valid ISBN" (fun (_: string) -> false)

            let failure =
                match Constraint.check isbn "x" with
                | Error failure -> failure
                | Ok() -> failwith "Expected the constraint to reject the value."

            // `books.isbn` and `books` are segments of one identity, not fallback levels of their own.
            let renderer = lookupOf [ "books.isbn", "WRONG"; "books", "ALSO WRONG" ]

            test <@ Violation.message renderer failure = "must be a valid ISBN" @>

    // ---------------------------------------------------------------------------------------------------
    // Cultures
    // ---------------------------------------------------------------------------------------------------

    module Cultures =
        let private resources () =
            Resources.ResourceManager("Axial.Tests.Messages", Reflection.Assembly.GetExecutingAssembly())

        [<Fact>]
        let ``one culture drives lookup and operand formatting together`` () =
            let renderer =
                Renderer.ofResourceManager (resources ()) (CultureInfo "de-DE")
                |> Renderer.attribute "amount"

            let violation =
                Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Decimal 1234.5M)), None))

            test <@ Violation.message renderer violation = "must be at least 1234,5" @>

        [<Fact>]
        let ``two cultures separate interface language from operand conventions`` () =
            let renderer =
                Renderer.ofResourceManagerWithCultures (resources ()) (CultureInfo "en") (CultureInfo "de-DE")

            let violation =
                Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Decimal 1234.5M)), None))

            test <@ Violation.message renderer violation = "must be at least 1234,5" @>

        [<Fact>]
        let ``the current-culture renderer reads the ambient culture on every render`` () =
            let renderer = Renderer.ofCurrentCulture (resources ())

            let violation =
                Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Decimal 1234.5M)), None))

            let original = CultureInfo.CurrentCulture

            try
                CultureInfo.CurrentCulture <- CultureInfo "de-DE"
                test <@ Violation.message renderer violation = "must be at least 1234,5" @>

                CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture
                test <@ Violation.message renderer violation = "must be at least 1234.5" @>
            finally
                CultureInfo.CurrentCulture <- original

    // ---------------------------------------------------------------------------------------------------
    // Catalogue coverage
    // ---------------------------------------------------------------------------------------------------

    module CatalogueCoverage =
        [<Fact>]
        let ``every key the atom catalogue can produce has a generated entry`` () =
            let produced =
                (KeyCatalogueDocTests.atoms () |> List.map ConstraintAtom.key)
                @ (KeyCatalogueDocTests.operations () |> List.map UnsupportedOperation.key)

            let missing = produced |> List.filter (fun key -> not (Catalogue.english.ContainsKey key))

            test <@ missing = [] @>

        [<Fact>]
        let ``every entry declares arguments its English template actually names`` () =
            let placeholders (template: string) =
                Text.RegularExpressions.Regex.Matches(template, @"(?<!\{)\{([A-Za-z]+)")
                |> Seq.map (fun placeholder -> placeholder.Groups[1].Value)
                |> Set.ofSeq

            let mismatched =
                Catalogue.keys
                |> List.filter (fun key ->
                    let declared = Catalogue.arguments[key] |> Set.ofList
                    let used = placeholders Catalogue.english[key]
                    not (Set.isSubset used declared))

            test <@ mismatched = [] @>

        [<Fact>]
        let ``every declared plural operand is one of the entry's arguments`` () =
            let invalid =
                Catalogue.keys
                |> List.filter (fun key ->
                    match Catalogue.pluralArgument[key] with
                    | Some operand -> not (Catalogue.arguments[key] |> List.contains operand)
                    | None -> false)

            test <@ invalid = [] @>

        [<Fact>]
        let ``every entry's key parses under the ordinary relative-key grammar`` () =
            let malformed =
                Catalogue.keys
                |> List.filter (fun key ->
                    match MessageDescriptor.Advanced.tryCreate key Map.empty with
                    | Ok _ -> false
                    | Error _ -> true)

            test <@ malformed = [] @>
