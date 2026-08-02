namespace Axial

/// <summary>Internal exact structural comparison for structured data.</summary>
[<RequireQualifiedAccess>]
module internal DataComparison =
    /// <summary>Returns all exact structural differences between two values.</summary>
    /// <example><code>Data.diff (data [ "name" =&gt; "Ada" ]) (data [ "name" =&gt; "Grace" ])
    /// // one DifferentValue difference at path "name"</code></example>
    let diff (expected: Data) (actual: Data) : DataDifference list =
        let difference path expected actual cause =
            { Path = path; Expected = expected; Actual = actual; Cause = cause }

        let rec compare path expected actual =
            match expected, actual with
            | _ when expected = actual -> []
            | Data.List expectedItems, Data.List actualItems ->
                let common = min expectedItems.Length actualItems.Length
                let shared = [ 0 .. common - 1 ] |> List.collect (fun index -> compare (appendPath (DataPathSegment.Index index) path) expectedItems[index] actualItems[index])
                let missing = [ common .. expectedItems.Length - 1 ] |> List.map (fun index -> difference (appendPath (DataPathSegment.Index index) path) (Some expectedItems[index]) None DataDifferenceCause.Missing)
                let unexpected = [ common .. actualItems.Length - 1 ] |> List.map (fun index -> difference (appendPath (DataPathSegment.Index index) path) None (Some actualItems[index]) DataDifferenceCause.Unexpected)
                shared @ missing @ unexpected
            | Data.Object expectedFields, Data.Object actualFields ->
                let common = min expectedFields.Length actualFields.Length
                let shared =
                    [ 0 .. common - 1 ]
                    |> List.collect (fun index ->
                        let expectedName, expectedValue = expectedFields[index]
                        let actualName, actualValue = actualFields[index]
                        let fieldPath = appendPath (DataPathSegment.Name expectedName) path
                        if expectedName <> actualName then
                            [ difference fieldPath (Some expectedValue) (Some actualValue) DataDifferenceCause.DifferentFieldName ]
                        else compare fieldPath expectedValue actualValue)
                let missing = [ common .. expectedFields.Length - 1 ] |> List.map (fun index -> let name, value = expectedFields[index] in difference (appendPath (DataPathSegment.Name name) path) (Some value) None DataDifferenceCause.Missing)
                let unexpected = [ common .. actualFields.Length - 1 ] |> List.map (fun index -> let name, value = actualFields[index] in difference (appendPath (DataPathSegment.Name name) path) None (Some value) DataDifferenceCause.Unexpected)
                shared @ missing @ unexpected
            | Data.List _, _
            | Data.Object _, _
            | _, Data.List _
            | _, Data.Object _ -> [ difference path (Some expected) (Some actual) DataDifferenceCause.DifferentShape ]
            | _ -> [ difference path (Some expected) (Some actual) DataDifferenceCause.DifferentValue ]

        compare DataPath.empty expected actual

    /// <summary>Compares complete values and returns every structural difference.</summary>
    /// <example><code>Data.compare (data [ "name" =&gt; "Ada" ]) (data [ "name" =&gt; "Ada" ])
    /// // Ok ()</code></example>
    let compare expected actual =
        match diff expected actual with
        | [] -> Ok()
        | differences -> Error differences

