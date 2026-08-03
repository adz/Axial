// The constraint message catalogue: one canonical source for every built-in identity's parsed key segments,
// argument names, neutral English predicate, and plural operand. Violation rendering and the public coverage
// data both read this table, so a rule cannot ship with its message undeclared.
namespace Axial.Constraint

/// <summary>One catalogue entry as authored.</summary>
type internal CatalogueEntry =
    { Segments: string list
      Arguments: string list
      English: string
      Plural: string option }

/// <summary>The built-in message catalogue, and what a translator must cover.</summary>
/// <remarks>
/// <para>
/// Entries are bare predicates: "must be at least {expected}", not a whole sentence. The attribute noun and the
/// optional actual-value clause are separate composition entries (<c>constraint.fullMessage</c> and
/// <c>constraint.actual</c>), so a locale can place either in its own order and <c>{actual}</c> needs no
/// optional-placeholder rule.
/// </para>
/// <para>
/// The composition and joining entries — <c>constraint.attribute.default</c>, <c>constraint.actual</c>,
/// <c>constraint.fullMessage</c>, <c>constraint.group.all.*</c>, <c>constraint.group.any.*</c>, and
/// <c>constraint.list.*</c> — are listed here too, because a language that reorders them must be able to find
/// them.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module Catalogue =
    let internal entries: CatalogueEntry list =
        let entry segments arguments plural english =
            { Segments = segments
              Arguments = arguments
              English = english
              Plural = plural }

        let constraint' rest arguments plural english =
            entry ("constraint" :: rest) arguments plural english

        let unsupported rest english =
            constraint' ("unsupportedOperand" :: rest) [] None english

        [ constraint' [ "presence"; "present" ] [] None "must be present"
          constraint' [ "presence"; "blank" ] [] None "must be blank"
          constraint' [ "cardinality"; "exact" ] [ "expected" ] (Some "expected") "must have a size of exactly {expected}"
          constraint' [ "cardinality"; "minimum" ] [ "minimum" ] (Some "minimum") "must have a size of at least {minimum}"
          constraint' [ "cardinality"; "maximum" ] [ "maximum" ] (Some "maximum") "must have a size of at most {maximum}"
          constraint'
              [ "cardinality"; "between" ]
              [ "minimum"; "maximum" ]
              None
              "must have a size between {minimum} and {maximum}"
          constraint' [ "relation"; "equal" ] [ "expected" ] None "must be {expected}"
          constraint' [ "relation"; "notEqual" ] [ "expected" ] None "must not be {expected}"
          constraint' [ "relation"; "greaterThan" ] [ "expected" ] None "must be greater than {expected}"
          constraint' [ "relation"; "lessThan" ] [ "expected" ] None "must be less than {expected}"
          constraint' [ "relation"; "atLeast" ] [ "expected" ] None "must be at least {expected}"
          constraint' [ "relation"; "atMost" ] [ "expected" ] None "must be at most {expected}"
          constraint' [ "relation"; "within" ] [ "minimum"; "maximum" ] None "must be between {minimum} and {maximum}"
          constraint' [ "membership"; "oneOf" ] [ "choices" ] None "must be one of {choices}"
          constraint' [ "membership"; "noneOf" ] [ "choices" ] None "must not be one of {choices}"
          constraint' [ "membership"; "contains" ] [ "item" ] None "must contain {item}"
          constraint' [ "membership"; "notContains" ] [ "item" ] None "must not contain {item}"
          constraint' [ "uniqueness" ] [] None "must not contain duplicate values"
          constraint' [ "format"; "email" ] [] None "must be an email address"
          constraint' [ "format"; "trimmed" ] [] None "must not have leading or trailing whitespace"
          constraint' [ "format"; "numeric" ] [] None "must contain digits only"
          constraint' [ "format"; "alphanumeric" ] [] None "must contain letters and digits only"
          constraint' [ "format"; "pattern" ] [ "pattern" ] None "must match {pattern}"
          constraint' [ "number"; "multipleOf" ] [ "divisor" ] None "must be a multiple of {divisor}"
          constraint' [ "number"; "finite" ] [] None "must be a finite number"

          unsupported [ "relation"; "equal" ] "failed an equality rule whose operand has no portable representation"
          unsupported [ "relation"; "notEqual" ] "failed an inequality rule whose operand has no portable representation"
          unsupported
              [ "relation"; "greaterThan" ]
              "failed a greater-than rule whose operand has no portable representation"
          unsupported [ "relation"; "lessThan" ] "failed a less-than rule whose operand has no portable representation"
          unsupported [ "relation"; "atLeast" ] "failed an at-least rule whose operand has no portable representation"
          unsupported [ "relation"; "atMost" ] "failed an at-most rule whose operand has no portable representation"
          unsupported [ "within" ] "failed a range rule whose operand has no portable representation"
          unsupported [ "contains" ] "failed a containment rule whose operand has no portable representation"
          unsupported [ "multipleOf" ] "failed a multiple-of rule whose operand has no portable representation"

          constraint' [ "attribute"; "default" ] [] None "value"
          constraint' [ "actual" ] [ "message"; "actual" ] None "{message}, but was {actual}"
          constraint' [ "fullMessage" ] [ "attribute"; "message" ] None "{attribute} {message}"

          constraint' [ "group"; "all"; "pair" ] [ "first"; "second" ] None "{first} and {second}"
          constraint' [ "group"; "all"; "start" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "group"; "all"; "middle" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "group"; "all"; "end" ] [ "first"; "second" ] None "{first} and {second}"

          constraint' [ "group"; "any"; "pair" ] [ "first"; "second" ] None "{first} or {second}"
          constraint' [ "group"; "any"; "start" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "group"; "any"; "middle" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "group"; "any"; "end" ] [ "first"; "second" ] None "{first} or {second}"

          constraint' [ "list"; "pair" ] [ "first"; "second" ] None "{first} and {second}"
          constraint' [ "list"; "start" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "list"; "middle" ] [ "first"; "rest" ] None "{first}, {rest}"
          constraint' [ "list"; "end" ] [ "first"; "second" ] None "{first} and {second}" ]

    let internal byKey =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry)
        |> Map.ofList

    /// Built from generated segments rather than reparsed on every render.
    let internal specOf (key: string) (arguments: Map<string, ConstraintValue>) =
        match byKey |> Map.tryFind key with
        | Some entry ->
            MessageDescriptor.ofSegments entry.Segments arguments
            |> MessageFormatSpec.ofParts entry.English entry.Plural
        | None ->
            // Unreachable for Axial-produced identities; an enumeration test proves it. Degrading to the key
            // itself beats throwing at a rendering edge.
            MessageDescriptor.ofSegments (key.Split '.' |> List.ofArray) arguments
            |> MessageFormatSpec.ofParts key None

    /// <summary>Every message key Axial can produce, including the composition and joining entries.</summary>
    /// <remarks>Enumerate this to test that a translation covers the base catalogue.</remarks>
    /// <example><code>Catalogue.keys |> List.filter (fun key -> not (translations.ContainsKey key))</code></example>
    let keys: string list =
        entries |> List.map (fun entry -> String.concat "." entry.Segments)

    /// <summary>The argument names each entry's template may interpolate.</summary>
    /// <remarks>
    /// <c>actual</c> is not listed: it never appears in a predicate. It reaches a message through the separate
    /// <c>constraint.actual</c> composition entry, which is what keeps it optional without an optional-placeholder
    /// rule.
    /// </remarks>
    /// <example><code>Catalogue.arguments.["constraint.cardinality.between"] // [ "minimum"; "maximum" ]</code></example>
    let arguments: Map<string, string list> =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry.Arguments)
        |> Map.ofList

    /// <summary>The neutral English template for each entry, used when no resource resolves.</summary>
    /// <example><code>Catalogue.english.["constraint.presence.present"] // "must be present"</code></example>
    let english: Map<string, string> =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry.English)
        |> Map.ofList

    /// <summary>The argument each entry may be pluralized on, when it declares one.</summary>
    /// <remarks>
    /// At most one per entry. A translation may supply <c>&lt;key&gt;.one</c> and <c>&lt;key&gt;.other</c> for
    /// these; every other entry takes a single form.
    /// </remarks>
    /// <example><code>Catalogue.pluralArgument.["constraint.cardinality.minimum"] // Some "minimum"</code></example>
    let pluralArgument: Map<string, string option> =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry.Plural)
        |> Map.ofList
