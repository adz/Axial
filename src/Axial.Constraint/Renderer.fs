// Renderer: the rendering edge. It owns document/attribute context, contextual candidate order, plural
// selection, named interpolation, value formatting, and the list/group joining patterns. It knows nothing
// about Violation or Schema — both push MessageFormatSpec values through the same mechanics.
namespace Axial.Constraint

open System

/// <summary>The ordinary resource lookup: an encoded resource key in, a translated template out.</summary>
/// <remarks>
/// This is the whole portable integration surface. A dictionary's <c>TryFind</c>, a JSON bundle, or a resource
/// manager wrapper all satisfy it. Axial owns the candidate order, so a lookup only ever answers "do you have this
/// exact key".
/// </remarks>
type MessageLookup = string -> string option

/// <summary>One contextual level's request to an advanced resolver.</summary>
/// <remarks>
/// <c>BaseKey</c> is an encoded contextual resource key with no plural suffix applied. A resolver that selects
/// plural categories itself reads <c>PluralArgument</c> and <c>Arguments</c> and answers for the whole level.
/// </remarks>
type MessageRequest =
    { /// <summary>The encoded contextual resource key, without a plural suffix.</summary>
      BaseKey: string
      /// <summary>The operands the entry may interpolate.</summary>
      Arguments: Map<string, ConstraintValue>
      /// <summary>The owning catalogue's plural operand, when it declares one.</summary>
      PluralArgument: string option }

/// <summary>What an advanced resolver found for one contextual level.</summary>
[<RequireQualifiedAccess>]
type MessageResolution =
    /// <summary>A template Axial should interpolate and format.</summary>
    | Template of string
    /// <summary>Text the resolver has already rendered. Axial never interpolates it again.</summary>
    | Rendered of string

/// <summary>Resolves one contextual level, or declines so Axial continues to a less specific one.</summary>
type MessageResolver = MessageRequest -> MessageResolution option

/// <summary>A value to format, with the placeholder's format suffix when it carried one.</summary>
type ValueFormatRequest =
    { /// <summary>The operand being formatted.</summary>
      Value: ConstraintValue
      /// <summary>The text after <c>:</c> in the placeholder, as in <c>{divisor:N0}</c>.</summary>
      Format: string option }

#if !FABLE_COMPILER
/// <summary>Culture-aware operand formatting for the .NET targets.</summary>
module internal ValueFormatting =
    let private standardNumeric =
        set [ 'B'; 'b'; 'C'; 'c'; 'D'; 'd'; 'E'; 'e'; 'F'; 'f'; 'G'; 'g'; 'N'; 'n'; 'P'; 'p'; 'R'; 'r'; 'X'; 'x' ]

    let private standardDate =
        set [ 'd'; 'D'; 'f'; 'F'; 'g'; 'G'; 'M'; 'm'; 'O'; 'o'; 'R'; 'r'; 's'; 't'; 'T'; 'u'; 'U'; 'Y'; 'y' ]

    let private standardSpan = set [ 'c'; 'g'; 'G' ]

    let private contains (specifiers: string) (format: string) =
        format |> Seq.exists (fun character -> specifiers.Contains character)

    // .NET does not reject an unrecognized format: `13L.ToString "zzz"` is "zzz", a nonsense message rather than
    // an exception. Recognizing the shapes the platform can honour is what turns a resource typo into ordinary
    // formatting instead of gibberish in front of a user.
    let private isSupported (value: ConstraintValue) (format: string) =
        let standard (letters: Set<char>) =
            format.Length >= 1
            && letters.Contains format.[0]
            && format.Substring 1 |> Seq.forall Char.IsDigit

        match value with
        | ConstraintValue.Integer _
        | ConstraintValue.BigInteger _
        | ConstraintValue.Decimal _
        | ConstraintValue.Float _
        | ConstraintValue.Float32 _ -> standard standardNumeric || contains "0#" format
        | ConstraintValue.DateTime _
        | ConstraintValue.DateTimeOffset _ ->
            (format.Length = 1 && standardDate.Contains format.[0]) || contains "dfFghHKmMstyz" format
        | ConstraintValue.TimeSpan _ ->
            (format.Length = 1 && standardSpan.Contains format.[0]) || contains "dhmsf" format
        | _ -> false

    /// Formats one value with a placeholder suffix, degrading to ordinary rendering for anything the platform
    /// cannot honour. Never throws for a resource defect.
    let apply (culture: Globalization.CultureInfo) (format: string) (value: ConstraintValue) =
        let attempt (formattable: IFormattable) =
            try
                formattable.ToString(format, culture)
            with _ ->
                ConstraintValue.render value

        if not (isSupported value format) then
            ConstraintValue.render value
        else
            match value with
            | ConstraintValue.Integer value -> attempt value
            | ConstraintValue.BigInteger value -> attempt value
            | ConstraintValue.Decimal value -> attempt value
            | ConstraintValue.Float value -> attempt value.Value
            | ConstraintValue.Float32 value -> attempt value.Value
            | ConstraintValue.DateTime value -> attempt value
            | ConstraintValue.DateTimeOffset value -> attempt value
            | ConstraintValue.TimeSpan value -> attempt value
            | value -> ConstraintValue.render value

    /// Ordinary culture-aware rendering with no suffix.
    let plain (culture: Globalization.CultureInfo) (value: ConstraintValue) =
        match value with
        | ConstraintValue.Integer value -> value.ToString culture
        | ConstraintValue.BigInteger value -> value.ToString culture
        | ConstraintValue.Decimal value -> value.ToString culture
        | ConstraintValue.Float value -> value.Value.ToString culture
        | ConstraintValue.Float32 value -> value.Value.ToString culture
        | ConstraintValue.DateTime value -> value.ToString culture
        | ConstraintValue.DateTimeOffset value -> value.ToString culture
        | ConstraintValue.TimeSpan value -> value.ToString("c", culture)
        | value -> ConstraintValue.render value
#endif

/// <summary>Named-placeholder parsing, shared identically by every target.</summary>
module internal MessageTemplate =
    type Part =
        | Literal of string
        | Placeholder of name: string * format: string option

    /// Returns None for unmatched or otherwise malformed braces. A malformed application template is a resource
    /// defect, not an exception: lookup continues through the normal fallback chain instead of throwing.
    let tryParse (template: string) : Part list option =
        let parts = ResizeArray<Part>()
        let literal = Text.StringBuilder()

        let flush () =
            if literal.Length > 0 then
                parts.Add(Literal(literal.ToString()))
                literal.Clear() |> ignore

        let rec go index =
            if index >= template.Length then
                flush ()
                Some(List.ofSeq parts)
            else
                match template.[index] with
                | '{' when index + 1 < template.Length && template.[index + 1] = '{' ->
                    literal.Append '{' |> ignore
                    go (index + 2)
                | '}' when index + 1 < template.Length && template.[index + 1] = '}' ->
                    literal.Append '}' |> ignore
                    go (index + 2)
                | '}' -> None
                | '{' ->
                    match template.IndexOf('}', index + 1) with
                    | -1 -> None
                    | close ->
                        let body = template.Substring(index + 1, close - index - 1)

                        if body = "" || body.Contains "{" then
                            None
                        else
                            flush ()

                            match body.IndexOf ':' with
                            | -1 -> parts.Add(Placeholder(body, None))
                            | separator when separator = 0 -> parts.Add(Placeholder(body, None))
                            | separator ->
                                parts.Add(
                                    Placeholder(
                                        body.Substring(0, separator),
                                        Some(body.Substring(separator + 1))
                                    )
                                )

                            go (close + 1)
                | character ->
                    literal.Append character |> ignore
                    go (index + 1)

        go 0

/// <summary>Turning an attribute path segment into a readable noun when no resource names it.</summary>
module internal AttributeHumanizer =
    let private isUpper (character: char) = Char.IsUpper character

    /// Splits camelCase, snake_case, and kebab-case, preserving acronym runs. `_id` is not stripped: a field
    /// genuinely named `customer_id` reads as "Customer id" rather than silently losing a word.
    let private words (segment: string) =
        let builder = Text.StringBuilder()

        for index in 0 .. segment.Length - 1 do
            let character = segment.[index]

            if character = '_' || character = '-' || character = ' ' then
                builder.Append ' ' |> ignore
            else
                let previous = if index > 0 then Some segment.[index - 1] else None
                let next = if index + 1 < segment.Length then Some segment.[index + 1] else None

                let boundary =
                    match previous with
                    | None -> false
                    | Some previous when previous = '_' || previous = '-' || previous = ' ' -> false
                    | Some previous ->
                        (isUpper character && not (isUpper previous))
                        || (isUpper character
                            && isUpper previous
                            && (match next with
                                | Some next -> Char.IsLower next
                                | None -> false))

                if boundary then builder.Append ' ' |> ignore
                builder.Append character |> ignore

        builder.ToString().Split ' '
        |> Array.filter (fun word -> word <> "")
        |> List.ofArray

    let private isAcronym (word: string) =
        word.Length > 1 && word |> Seq.forall (fun character -> not (Char.IsLower character))

    /// Invariant sentence casing. Acronym runs keep their spelling; everything else lowercases, and the first
    /// word takes an initial capital.
    let humanize (segment: string) =
        match words segment with
        | [] -> segment
        | first :: rest ->
            let normalize (word: string) =
                if isAcronym word then word else word.ToLowerInvariant()

            let head =
                if isAcronym first then
                    first
                else
                    let lowered = first.ToLowerInvariant()
                    string (Char.ToUpperInvariant lowered.[0]) + lowered.Substring 1

            head :: (rest |> List.map normalize) |> String.concat " "

/// <summary>The composition entries the renderer owns, with their neutral English fallbacks.</summary>
module internal CompositionCatalogue =
    let private spec key plural arguments fallback =
        MessageDescriptor.ofSegments key arguments
        |> MessageFormatSpec.ofParts fallback plural

    let attributeDefault () =
        spec [ "constraint"; "attribute"; "default" ] None Map.empty "value"

    let actual message actual =
        spec
            [ "constraint"; "actual" ]
            None
            (Map [ "message", ConstraintValue.Text message; "actual", actual ])
            "{message}, but was {actual}"

    let fullMessage attribute message =
        spec
            [ "constraint"; "fullMessage" ]
            None
            (Map [ "attribute", ConstraintValue.Text attribute; "message", ConstraintValue.Text message ])
            "{attribute} {message}"

    /// The pair/start/middle/end family for one joining group, with the English wording each shape needs.
    type Patterns =
        { Segments: string list
          Pair: string
          Start: string
          Middle: string
          End: string }

    let all =
        { Segments = [ "constraint"; "group"; "all" ]
          Pair = "{first} and {second}"
          Start = "{first}, {rest}"
          Middle = "{first}, {rest}"
          End = "{first} and {second}" }

    let any =
        { Segments = [ "constraint"; "group"; "any" ]
          Pair = "{first} or {second}"
          Start = "{first}, {rest}"
          Middle = "{first}, {rest}"
          End = "{first} or {second}" }

    let list =
        { Segments = [ "constraint"; "list" ]
          Pair = "{first} and {second}"
          Start = "{first}, {rest}"
          Middle = "{first}, {rest}"
          End = "{first} and {second}" }

    let pattern (patterns: Patterns) (shape: string) (first: string) (secondName: string) (second: string) =
        let fallback =
            match shape with
            | "pair" -> patterns.Pair
            | "start" -> patterns.Start
            | "middle" -> patterns.Middle
            | _ -> patterns.End

        spec
            (patterns.Segments @ [ shape ])
            None
            (Map [ "first", ConstraintValue.Text first; secondName, ConstraintValue.Text second ])
            fallback

/// <summary>
/// Renders localized messages for one document context and attribute. Immutable: build one at the composition
/// root and derive scoped copies with <c>context</c> and <c>attribute</c>.
/// </summary>
/// <remarks>
/// <para>
/// A renderer holds no violation and a violation holds no renderer. Context arrives here, at the rendering edge,
/// which is what keeps <c>Violation</c> path-free, closure-free comparable data.
/// </para>
/// <para>
/// <c>context</c> appends a document, model, form, or component segment. <c>attribute</c> replaces the whole
/// attribute with one segment, so a form-scoped renderer is safe to reuse for sibling fields without leaking the
/// previous field's noun.
/// </para>
/// </remarks>
[<Sealed; AllowNullLiteral>]
type Renderer
    internal (context: string list, attribute: string list option, resolve: MessageResolver, formatValue: ValueFormatRequest -> string) =
    member internal _.Context = context
    member internal _.Attribute = attribute
    member internal _.Resolve = resolve
    member internal _.FormatValue = formatValue

/// <summary>Builds renderers and renders messages through them.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Renderer =
    let private isOne (value: ConstraintValue) =
        match value with
        | ConstraintValue.Integer value -> value = 1L
        | ConstraintValue.BigInteger value -> value = bigint 1
        | ConstraintValue.Decimal value -> value = 1M
        | ConstraintValue.Float value -> value.Value = 1.0
        | ConstraintValue.Float32 value -> value.Value = 1.0f
        | _ -> false

    let internal pluralSuffix (plural: string option) (arguments: Map<string, ConstraintValue>) =
        plural
        |> Option.map (fun name ->
            match arguments |> Map.tryFind name with
            | Some value when isOne value -> ".one"
            | _ -> ".other")

    /// Invariant value formatting, used by every portable constructor.
    /// <remarks>
    /// Under Fable there is no culture machinery to format through, so a suffix is ignored and the ordinary
    /// portable rendering stands. An application that needs suffixes there supplies
    /// <c>Renderer.Advanced.withValueFormatting</c>.
    /// </remarks>
    let internal invariantFormat (request: ValueFormatRequest) =
#if FABLE_COMPILER
        ConstraintValue.render request.Value
#else
        let culture = Globalization.CultureInfo.InvariantCulture

        match request.Format with
        | None
        | Some "" -> ConstraintValue.render request.Value
        | Some format -> ValueFormatting.apply culture format request.Value
#endif

    /// The contextual prefixes, most specific first, ending with the unscoped level. Each is already encoded.
    let internal levels (renderer: Renderer) =
        let combined =
            renderer.Context
            @ (renderer.Attribute |> Option.defaultValue [])
            |> List.map MessageSegment.encode

        let rec drop segments acc =
            match segments with
            | [] -> List.rev ("" :: acc)
            | _ ->
                let prefix = String.concat "." segments
                drop (segments |> List.take (segments.Length - 1)) (prefix :: acc)

        drop combined []

    let internal qualify (level: string) (key: string) = if level = "" then key else level + "." + key

    /// <summary>The renderer's own resolver, used unchanged for every request.</summary>
    let internal resolveWith (renderer: Renderer) request = renderer.Resolve request

    /// The composition entries substitute text this renderer has already produced. Those arguments must not be
    /// passed through the value formatter: a `withValues` callback would rewrite a finished message.
    let private composed =
        set [ "message"; "attribute"; "first"; "second"; "rest" ]

    let rec internal formatArgument (renderer: Renderer) (format: string option) (value: ConstraintValue) =
        match value with
        | ConstraintValue.List items ->
            items
            |> List.map (formatArgument renderer format)
            |> joinPattern renderer CompositionCatalogue.list
        | value -> renderer.FormatValue { Value = value; Format = format }

    and internal interpolate
        (renderer: Renderer)
        (literals: Set<string>)
        (template: string)
        (arguments: Map<string, ConstraintValue>)
        =
        MessageTemplate.tryParse template
        |> Option.map (fun parts ->
            parts
            |> List.map (fun part ->
                match part with
                | MessageTemplate.Literal text -> text
                | MessageTemplate.Placeholder(name, format) ->
                    match arguments |> Map.tryFind name with
                    | Some(ConstraintValue.Text text) when literals.Contains name -> text
                    | Some value -> formatArgument renderer format value
                    // An unknown placeholder name stays literal. A translator's typo should show up in the
                    // message, not take down the request.
                    | None ->
                        match format with
                        | Some format -> "{" + name + ":" + format + "}"
                        | None -> "{" + name + "}")
            |> String.concat "")

    and internal formatSpecWith (renderer: Renderer) (literals: Set<string>) (spec: MessageFormatSpec) : string =
        let descriptor = MessageFormatSpec.descriptor spec
        let arguments = MessageDescriptor.arguments descriptor
        let baseKey = MessageDescriptor.encodedKey descriptor
        let plural = MessageFormatSpec.pluralArgument spec
        let fallback = MessageFormatSpec.fallback spec

        let rec go remaining =
            match remaining with
            | [] ->
                interpolate renderer literals fallback arguments
                |> Option.defaultValue fallback
            | level :: rest ->
                let request =
                    { BaseKey = qualify level baseKey
                      Arguments = arguments
                      PluralArgument = plural }

                match renderer.Resolve request with
                | Some(MessageResolution.Rendered text) -> text
                | Some(MessageResolution.Template template) ->
                    match interpolate renderer literals template arguments with
                    | Some text -> text
                    | None -> go rest
                | None -> go rest

        go (levels renderer)

    and internal formatSpec (renderer: Renderer) (spec: MessageFormatSpec) : string =
        formatSpecWith renderer Set.empty spec

    /// Deterministic joining: empty is empty, a singleton is itself, two use `pair`, and three or more combine
    /// the last two with `end` then fold leftwards through `middle` and `start`.
    and internal joinPattern (renderer: Renderer) (patterns: CompositionCatalogue.Patterns) (items: string list) =
        let compose shape first secondName second =
            formatSpecWith renderer composed (CompositionCatalogue.pattern patterns shape first secondName second)

        match items with
        | [] -> ""
        | [ single ] -> single
        | [ first; second ] -> compose "pair" first "second" second
        | first :: rest ->
            let rec tail items =
                match items with
                | [ left; right ] -> compose "end" left "second" right
                | left :: rest -> compose "middle" left "rest" (tail rest)
                | [] -> ""

            compose "start" first "rest" (tail rest)

    let internal attributeKeys (renderer: Renderer) =
        match renderer.Attribute with
        | None
        | Some [] -> []
        | Some attribute ->
            let context = renderer.Context

            let contextual =
                [ 0 .. List.length context ]
                |> List.map (fun drop -> (context |> List.skip drop) @ attribute)

            let narrowing =
                [ 1 .. List.length attribute - 1 ]
                |> List.map (fun drop -> attribute |> List.skip drop)

            contextual @ narrowing
            |> List.map (fun segments -> "attribute." + MessageSegment.join segments)
            |> List.distinct

    /// <summary>A renderer that uses each catalogue's neutral English, with no resources at all.</summary>
    /// <remarks>
    /// The default for tests, tools, and applications that never translate. It is not the same as
    /// <c>Violation.render</c>: this produces bare predicates that compose, while <c>render</c> keeps the legacy
    /// self-contained English exactly.
    /// </remarks>
    /// <example><code>violation |> Violation.fullMessage Renderer.english // "value must be present"</code></example>
    let english = Renderer([], None, (fun _ -> None), invariantFormat)

    /// <summary>A renderer backed by any key-to-template lookup.</summary>
    /// <remarks>The portable constructor, and the one Fable applications use.</remarks>
    /// <example><code>let renderer = Renderer.ofLookup translations.TryFind</code></example>
    let ofLookup (lookup: MessageLookup) : Renderer =
        if isNull (box lookup) then
            nullArg (nameof lookup)

        let resolve (request: MessageRequest) =
            let plural =
                pluralSuffix request.PluralArgument request.Arguments
                |> Option.bind (fun suffix -> lookup (request.BaseKey + suffix))

            match plural with
            | Some template -> Some(MessageResolution.Template template)
            // A bare field-specific entry beats a pluralized model-level entry because the bare key is tried at
            // this level before moving outwards, not after the whole plural pass.
            | None -> lookup request.BaseKey |> Option.map MessageResolution.Template

        Renderer([], None, resolve, invariantFormat)

#if !FABLE_COMPILER
    let private ofCultureFunctions (resources: Resources.ResourceManager) (uiCulture: unit -> Globalization.CultureInfo) (valueCulture: unit -> Globalization.CultureInfo) =
        if isNull resources then nullArg (nameof resources)

        let lookup (key: string) =
            // GetString returns null for a missing entry; a missing satellite assembly or resource set is a
            // resource miss, not an application defect, so it falls through to the next candidate.
            match resources.GetString(key, uiCulture ()) with
            | null -> None
            | value -> Some value

        let resolve (request: MessageRequest) =
            let plural =
                pluralSuffix request.PluralArgument request.Arguments
                |> Option.bind (fun suffix -> lookup (request.BaseKey + suffix))

            match plural with
            | Some template -> Some(MessageResolution.Template template)
            | None -> lookup request.BaseKey |> Option.map MessageResolution.Template

        let formatValue (request: ValueFormatRequest) =
            let culture = valueCulture ()

            match request.Format with
            | None
            | Some "" -> ValueFormatting.plain culture request.Value
            | Some format -> ValueFormatting.apply culture format request.Value

        Renderer([], None, resolve, formatValue)

    /// <summary>A renderer backed by a .NET resource manager, using one culture for everything.</summary>
    /// <remarks>The culture drives resource lookup, ordinary plural selection, and number and date formatting.</remarks>
    /// <example><code>let renderer = Renderer.ofResourceManager resources (CultureInfo "fr-FR")</code></example>
    let ofResourceManager (resources: Resources.ResourceManager) (culture: Globalization.CultureInfo) : Renderer =
        if isNull culture then nullArg (nameof culture)
        ofCultureFunctions resources (fun () -> culture) (fun () -> culture)

    /// <summary>A renderer that looks messages up in one culture and formats operands in another.</summary>
    /// <remarks>
    /// The split a UI needs when the interface language and the reader's number and date conventions differ —
    /// English text with German decimal separators, for instance.
    /// </remarks>
    /// <example><code>Renderer.ofResourceManagerWithCultures resources (CultureInfo "en") (CultureInfo "de-DE")</code></example>
    let ofResourceManagerWithCultures
        (resources: Resources.ResourceManager)
        (uiCulture: Globalization.CultureInfo)
        (valueCulture: Globalization.CultureInfo)
        : Renderer =
        if isNull uiCulture then nullArg (nameof uiCulture)
        if isNull valueCulture then nullArg (nameof valueCulture)
        ofCultureFunctions resources (fun () -> uiCulture) (fun () -> valueCulture)

    /// <summary>A renderer that reads the ambient cultures at each render rather than capturing them.</summary>
    /// <remarks>
    /// <c>CurrentUICulture</c> drives lookup and plural selection; <c>CurrentCulture</c> drives operand
    /// formatting. Both are read per render, so one renderer registered as a singleton follows a per-request
    /// culture. This is the one place ambient culture enters Axial: constraint execution stays effect-free.
    /// </remarks>
    /// <example><code>let renderer = Renderer.ofCurrentCulture resources</code></example>
    let ofCurrentCulture (resources: Resources.ResourceManager) : Renderer =
        ofCultureFunctions
            resources
            (fun () -> Globalization.CultureInfo.CurrentUICulture)
            (fun () -> Globalization.CultureInfo.CurrentCulture)
#endif

    /// <summary>Appends a document, model, form, or component segment.</summary>
    /// <example><code>let signup = renderer |> Renderer.context "signup"</code></example>
    let context (segment: string) (renderer: Renderer) : Renderer =
        if isNull renderer then nullArg (nameof renderer)
        if isNull segment || segment = "" then invalidArg (nameof segment) "A context segment cannot be empty."

        Renderer(renderer.Context @ [ segment ], renderer.Attribute, renderer.Resolve, renderer.FormatValue)

    /// <summary>Replaces the attribute with one segment.</summary>
    /// <remarks>
    /// Replacement, not append: a form-scoped renderer stays reusable for sibling fields. Schema supplies its
    /// typed path through <c>Renderer.Advanced.attributePath</c> instead.
    /// </remarks>
    /// <example><code>signup |> Renderer.attribute "name"</code></example>
    let attribute (segment: string) (renderer: Renderer) : Renderer =
        if isNull renderer then nullArg (nameof renderer)

        if isNull segment || segment = "" then
            invalidArg (nameof segment) "An attribute segment cannot be empty."

        Renderer(renderer.Context, Some [ segment ], renderer.Resolve, renderer.FormatValue)

    /// <summary>Clears both the context and the attribute.</summary>
    /// <example><code>let bare = signup |> Renderer.unscoped</code></example>
    let unscoped (renderer: Renderer) : Renderer =
        if isNull renderer then nullArg (nameof renderer)
        Renderer([], None, renderer.Resolve, renderer.FormatValue)

    /// <summary>Replaces all operand rendering with one callback, ignoring placeholder format suffixes.</summary>
    /// <remarks>
    /// List operands still join through the contextual <c>constraint.list.*</c> patterns; the callback renders
    /// each item. Use <c>Renderer.Advanced.withValueFormatting</c> when the suffix matters.
    /// </remarks>
    /// <example><code>renderer |> Renderer.withValues (fun value -> ConstraintValue.render value)</code></example>
    let withValues (format: ConstraintValue -> string) (renderer: Renderer) : Renderer =
        if isNull renderer then nullArg (nameof renderer)
        if isNull (box format) then nullArg (nameof format)

        Renderer(renderer.Context, renderer.Attribute, renderer.Resolve, (fun request -> format request.Value))

    /// <summary>The attribute noun this renderer composes into a full message.</summary>
    /// <remarks>
    /// Resolves <c>attribute.*</c> resources from most to least specific, then humanizes the final raw attribute
    /// segment. With no attribute it resolves the contextual <c>constraint.attribute.default</c>.
    /// </remarks>
    /// <example><code>signup |> Renderer.attribute "postcodeID" |> Renderer.attributeName // "Postcode ID"</code></example>
    let attributeName (renderer: Renderer) : string =
        if isNull renderer then nullArg (nameof renderer)

        let resolved =
            attributeKeys renderer
            |> List.tryPick (fun key ->
                let request =
                    { BaseKey = key
                      Arguments = Map.empty
                      PluralArgument = None }

                match renderer.Resolve request with
                | Some(MessageResolution.Rendered text) -> Some text
                | Some(MessageResolution.Template template) -> interpolate renderer Set.empty template Map.empty
                | None -> None)

        match resolved with
        | Some name -> name
        | None ->
            match renderer.Attribute with
            | Some attribute when not attribute.IsEmpty -> AttributeHumanizer.humanize (List.last attribute)
            | _ -> formatSpec renderer (CompositionCatalogue.attributeDefault ())

    /// <summary>Composes the attribute noun once around an already-rendered message.</summary>
    /// <remarks>
    /// <c>Violation.fullMessage</c> is this applied to <c>Violation.message</c>. It is public so another
    /// catalogue — Schema's, or your own — composes nouns through the same <c>constraint.fullMessage</c> entry
    /// rather than concatenating a noun itself.
    /// </remarks>
    /// <example><code>renderer |> Renderer.fullMessage "must be supplied" // "Postcode must be supplied"</code></example>
    let fullMessage (message: string) (renderer: Renderer) : string =
        if isNull renderer then nullArg (nameof renderer)
        formatSpecWith renderer composed (CompositionCatalogue.fullMessage (attributeName renderer) message)

    let internal composeActual (renderer: Renderer) (message: string) (actual: ConstraintValue) =
        formatSpecWith renderer composed (CompositionCatalogue.actual message actual)

    let internal composeFull (renderer: Renderer) (message: string) =
        formatSpecWith renderer composed (CompositionCatalogue.fullMessage (attributeName renderer) message)

    let internal joinAll (renderer: Renderer) items =
        joinPattern renderer CompositionCatalogue.all items

    let internal joinAny (renderer: Renderer) items =
        joinPattern renderer CompositionCatalogue.any items

    /// <summary>Replacing lookup, formatting, or inspecting the exact keys Axial will ask for.</summary>
    [<RequireQualifiedAccess>]
    module Advanced =
        /// <summary>A renderer backed by a resolver that answers one contextual level at a time.</summary>
        /// <remarks>
        /// Use this for ICU or any system that selects plural categories and renders entries itself. Axial still
        /// owns contextual fallback and violation composition; a system that must reorder a whole group takes
        /// <c>Violation.toMessageTree</c> instead.
        /// </remarks>
        /// <example><code>Renderer.Advanced.ofResolver (fun request -> icu.TryRender(request.BaseKey, request.Arguments))</code></example>
        let ofResolver (resolver: MessageResolver) : Renderer =
            if isNull (box resolver) then
                nullArg (nameof resolver)

            Renderer([], None, resolver, invariantFormat)

        /// <summary>Replaces operand formatting with a callback that receives the placeholder's format suffix.</summary>
        /// <example><code>renderer |> Renderer.Advanced.withValueFormatting (fun request -> myFormat request.Value request.Format)</code></example>
        let withValueFormatting (format: ValueFormatRequest -> string) (renderer: Renderer) : Renderer =
            if isNull renderer then nullArg (nameof renderer)
            if isNull (box format) then nullArg (nameof format)

            Renderer(renderer.Context, renderer.Attribute, renderer.Resolve, format)

        /// <summary>Sets the attribute to a complete path, replacing any previous one.</summary>
        /// <remarks>Schema supplies its typed <c>Path</c> keys through this; an empty list clears the attribute.</remarks>
        /// <example><code>renderer |> Renderer.Advanced.attributePath [ "address"; "postcode" ]</code></example>
        let attributePath (segments: string list) (renderer: Renderer) : Renderer =
            if isNull renderer then nullArg (nameof renderer)
            if isNull (box segments) then nullArg (nameof segments)

            if segments |> List.exists (fun segment -> isNull segment || segment = "") then
                invalidArg (nameof segments) "An attribute segment cannot be empty."

            let attribute = if segments.IsEmpty then None else Some segments
            Renderer(renderer.Context, attribute, renderer.Resolve, renderer.FormatValue)

        /// <summary>Every encoded resource key ordinary lookup will try, in order.</summary>
        /// <remarks>
        /// Includes the selected <c>.one</c>/<c>.other</c> key and the bare key at each contextual level. Use it
        /// to test translation coverage for contexts and fields Axial cannot enumerate for you.
        /// </remarks>
        /// <example><code>Renderer.Advanced.lookupCandidates renderer spec |> List.head</code></example>
        let lookupCandidates (renderer: Renderer) (spec: MessageFormatSpec) : string list =
            if isNull renderer then nullArg (nameof renderer)

            let descriptor = MessageFormatSpec.descriptor spec
            let baseKey = MessageDescriptor.encodedKey descriptor

            let suffix =
                pluralSuffix (MessageFormatSpec.pluralArgument spec) (MessageDescriptor.arguments descriptor)

            levels renderer
            |> List.collect (fun level ->
                let qualified = qualify level baseKey

                match suffix with
                | Some suffix -> [ qualified + suffix; qualified ]
                | None -> [ qualified ])

        /// <summary>One request per contextual level, as an advanced resolver receives them.</summary>
        /// <example><code>Renderer.Advanced.messageRequests renderer spec |> List.map _.BaseKey</code></example>
        let messageRequests (renderer: Renderer) (spec: MessageFormatSpec) : MessageRequest list =
            if isNull renderer then nullArg (nameof renderer)

            let descriptor = MessageFormatSpec.descriptor spec
            let baseKey = MessageDescriptor.encodedKey descriptor

            levels renderer
            |> List.map (fun level ->
                { BaseKey = qualify level baseKey
                  Arguments = MessageDescriptor.arguments descriptor
                  PluralArgument = MessageFormatSpec.pluralArgument spec })

        /// <summary>Every encoded attribute-noun key, most specific first.</summary>
        /// <example><code>Renderer.Advanced.attributeCandidates renderer // [ "attribute.signup.postcode"; "attribute.postcode" ]</code></example>
        let attributeCandidates (renderer: Renderer) : string list =
            if isNull renderer then nullArg (nameof renderer)
            attributeKeys renderer

        /// <summary>Renders any catalogue's entry through the full contextual, plural, and formatting path.</summary>
        /// <remarks>
        /// The entry point another package's catalogue uses. Schema renders its <c>schema.*</c> entries with this
        /// and adds no Schema knowledge to <c>Axial.Constraint</c>.
        /// </remarks>
        /// <example><code>renderer |> Renderer.Advanced.format spec</code></example>
        let format (spec: MessageFormatSpec) (renderer: Renderer) : string =
            if isNull renderer then nullArg (nameof renderer)
            formatSpec renderer spec
