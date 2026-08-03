namespace Axial.Tests

open System.IO
open System.Text.RegularExpressions
open Axial.Constraint
open Swensen.Unquote
open Xunit

/// <summary>
/// Holds the published key catalogue to the atom union and the generated catalogue it is derived from.
/// </summary>
/// <remarks>
/// The localization page is only useful if it is complete: a translator populating a resource file from it has no
/// other way to discover a key, and a missing entry surfaces as an untranslated message in production rather than
/// as a build failure. Deriving the assertion from the union rather than from a second hand-written list is the
/// same discipline that keeps an atom's description and its violation from drifting.
/// </remarks>
module KeyCatalogueDocTests =
    let private page () =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "docs", "values", "constraint", "localization.md")
        |> Path.GetFullPath

    /// Every key the atom catalogue can produce. Operands are irrelevant to the key, so one representative value
    /// per case is enough; what matters is that every case is listed.
    let internal atoms () =
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

    let internal operations () =
        [ UnsupportedOperation.Relation Equal
          UnsupportedOperation.Relation NotEqual
          UnsupportedOperation.Relation GreaterThan
          UnsupportedOperation.Relation LessThan
          UnsupportedOperation.Relation AtLeast
          UnsupportedOperation.Relation AtMost
          UnsupportedOperation.Within
          UnsupportedOperation.Contains
          UnsupportedOperation.MultipleOf ]

    /// One documented row: key, arguments, plural operand. The page spells every catalogue table the same way, so
    /// one pattern reads all three.
    let private rows () =
        Regex.Matches(
            File.ReadAllText(page ()),
            @"^\| `(constraint\.[^`]+)` \| ([^|]+) \| ([^|]+) \|",
            RegexOptions.Multiline
        )
        |> Seq.map (fun row ->
            let names (cell: string) =
                match cell.Trim() with
                | "—" -> Set.empty
                | cell -> cell.Split ',' |> Array.map (fun name -> name.Trim().Trim '`') |> Set.ofArray

            row.Groups[1].Value, (names row.Groups[2].Value, names row.Groups[3].Value))
        |> Map.ofSeq

    [<Fact>]
    let ``every key the atom catalogue can produce has a generated entry`` () =
        let produced =
            (atoms () |> List.map ConstraintAtom.key) @ (operations () |> List.map UnsupportedOperation.key)

        test <@ produced |> List.filter (Catalogue.english.ContainsKey >> not) = [] @>

    [<Fact>]
    let ``every generated key is documented`` () =
        let undocumented =
            Set.difference (Set.ofList Catalogue.keys) (rows () |> Map.toList |> List.map fst |> Set.ofList)

        test <@ undocumented = Set.empty @>

    [<Fact>]
    let ``the page documents no key the catalogue does not hold`` () =
        let stale =
            Set.difference (rows () |> Map.toList |> List.map fst |> Set.ofList) (Set.ofList Catalogue.keys)

        test <@ stale = Set.empty @>

    [<Fact>]
    let ``every documented argument and plural operand matches the generated entry`` () =
        // A template interpolating an argument that never arrives renders a placeholder to a user, and a plural
        // operand the page invents sends a translator off to write `.one` keys nothing will ever ask for.
        let mismatched =
            rows ()
            |> Map.toList
            |> List.filter (fun (key, (arguments, plural)) ->
                let declared = Catalogue.arguments[key] |> Set.ofList

                let declaredPlural =
                    Catalogue.pluralArgument[key] |> Option.map Set.singleton |> Option.defaultValue Set.empty

                arguments <> declared || plural <> declaredPlural)
            |> List.map fst

        test <@ mismatched = [] @>

    [<Fact>]
    let ``every atom's own arguments match its documented row`` () =
        let documented = rows ()

        let missing =
            atoms ()
            |> List.filter (fun atom ->
                let key = ConstraintAtom.key atom
                let supplied = ConstraintAtom.arguments atom |> Map.toList |> List.map fst |> Set.ofList

                match documented |> Map.tryFind key with
                | Some(arguments, _) -> arguments <> supplied
                | None -> true)
            |> List.map ConstraintAtom.key

        test <@ missing = [] @>
