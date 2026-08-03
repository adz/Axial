namespace Axial.Tests

open System.IO
open System.Text.RegularExpressions
open Axial.Constraint
open Swensen.Unquote
open Xunit

/// <summary>
/// Holds the published key catalogue to the atom union it is derived from.
/// </summary>
/// <remarks>
/// The localization page is only useful if it is complete: a translator populating a resource file from it has no
/// other way to discover a key, and a missing entry surfaces as an untranslated message in production rather than
/// as a build failure. Deriving the assertion from the union rather than from a second hand-written list is the
/// same discipline that keeps an atom's description and its violation from drifting.
/// </remarks>
module KeyCatalogueDocTests =
    let private page () =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "docs", "error-handling", "constraint", "localization.md")
        |> Path.GetFullPath

    /// Every key the atom catalogue can produce. Operands are irrelevant to the key, so one representative value
    /// per case is enough; what matters is that every case is listed.
    let private atoms () =
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

    let private operations () =
        [ UnsupportedOperation.Relation Equal
          UnsupportedOperation.Relation NotEqual
          UnsupportedOperation.Relation GreaterThan
          UnsupportedOperation.Relation LessThan
          UnsupportedOperation.Relation AtLeast
          UnsupportedOperation.Relation AtMost
          UnsupportedOperation.Within
          UnsupportedOperation.Contains
          UnsupportedOperation.MultipleOf ]

    /// Keys as the page spells them: the first cell of a table row, wrapped in backticks.
    let private documentedKeys () =
        Regex.Matches(File.ReadAllText (page ()), @"^\| `(constraint\.[^`]+)` \|", RegexOptions.Multiline)
        |> Seq.map (fun row -> row.Groups[1].Value)
        |> Set.ofSeq

    [<Fact>]
    let ``every key the catalogue can produce is documented`` () =
        let produced =
            Set.union
                (atoms () |> List.map ConstraintAtom.key |> Set.ofList)
                (operations () |> List.map UnsupportedOperation.key |> Set.ofList)

        let undocumented = Set.difference produced (documentedKeys ())

        test <@ undocumented = Set.empty @>

    [<Fact>]
    let ``the page documents no key the catalogue cannot produce`` () =
        let produced =
            Set.union
                (atoms () |> List.map ConstraintAtom.key |> Set.ofList)
                (operations () |> List.map UnsupportedOperation.key |> Set.ofList)

        let stale = Set.difference (documentedKeys ()) produced

        test <@ stale = Set.empty @>

    [<Fact>]
    let ``every documented argument name is one the atom actually supplies`` () =
        // A template interpolating an argument that never arrives renders a placeholder to a user. Checking the
        // names against `arguments` is what stops the table drifting from the payload.
        let missing =
            atoms ()
            |> List.collect (fun atom ->
                let key = ConstraintAtom.key atom
                let supplied = ConstraintAtom.arguments atom |> Map.toList |> List.map fst |> Set.ofList

                let documented =
                    Regex.Match(File.ReadAllText (page ()), $@"^\| `{Regex.Escape key}` \| ([^|]+) \|", RegexOptions.Multiline)
                    |> fun row -> row.Groups[1].Value.Trim()

                if documented = "—" then
                    if Set.isEmpty supplied then [] else [ key ]
                else
                    let named =
                        documented.Split ','
                        |> Array.map (fun name -> name.Trim().Trim '`')
                        |> Set.ofArray

                    if named = supplied then [] else [ key ])

        test <@ missing = [] @>
