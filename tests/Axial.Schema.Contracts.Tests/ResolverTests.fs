namespace Axial.Schema.Contracts.Tests

open Axial.Schema.Contracts
open Swensen.Unquote
open Xunit

module ResolverTests =

    let private parse name source =
        match Parser.parse name source with
        | Ok file -> file
        | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

    let private resolveOne source = Resolver.resolve [ parse "test.contract" source ]

    let private messages diagnostics =
        diagnostics |> List.map (fun diagnostic -> diagnostic.Message)

    [<Fact>]
    let ``a clean contract set resolves without diagnostics`` () =
        let diagnostics =
            resolveOne
                """
contract Geo.v1 {
  lat: decimal [ >= -90, <= 90 ]
}

contract Site.v1 {
  location?: Geo.v1
  name: text [ min 1 ]
}
"""

        test <@ diagnostics = [] @>

    [<Fact>]
    let ``unknown and version-mismatched references are reported`` () =
        let diagnostics =
            resolveOne
                """
contract Geo.v2 {
  lat: decimal
}

contract Site.v1 {
  a?: Missing.v1
  b?: Geo.v1
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "unknown contract reference 'Missing.v1'") @>
        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "declared at v2, but the reference pins v1") @>

    [<Fact>]
    let ``references must be declared before use within a file`` () =
        let diagnostics =
            resolveOne
                """
contract Site.v1 {
  location?: Geo.v1
}

contract Geo.v1 {
  lat: decimal
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "'Geo.v1' must be declared before 'Site.v1'") @>

    [<Fact>]
    let ``a contract may refer to its own pinned version`` () =
        let diagnostics =
            resolveOne
                """
contract Category.v1 {
  name: text
  children: list Category.v1
}
"""

        test <@ diagnostics = [] @>

    [<Fact>]
    let ``a contiguous ascending version chain resolves cleanly`` () =
        let diagnostics =
            resolveOne
                """
contract Config.v1 {
  a: int
}

contract Config.v2 {
  a: int
  b: int
}

contract Wrapper.v1 {
  old: Config.v1
  current: Config.v2
}
"""

        test <@ diagnostics = [] @>

    [<Fact>]
    let ``version chains must be declared oldest to newest with no gaps`` () =
        let gapped =
            resolveOne
                """
contract Config.v1 {
  a: int
}

contract Config.v3 {
  a: int
}
"""

        test <@ messages gapped |> List.exists (fun message -> message.Contains "no gaps") @>

        let descending =
            resolveOne
                """
contract Config.v2 {
  a: int
}

contract Config.v1 {
  a: int
}
"""

        test <@ messages descending |> List.exists (fun message -> message.Contains "no gaps") @>

    [<Fact>]
    let ``version chains cannot span files`` () =
        let diagnostics =
            Resolver.resolve
                [ parse "a.contract" "contract Config.v1 {\n  a: int\n}"
                  parse "b.contract" "contract Config.v2 {\n  a: int\n}" ]

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "must live in one file") @>

    [<Fact>]
    let ``a later version cannot be referenced before it is declared`` () =
        let diagnostics =
            resolveOne
                """
contract Config.v1 {
  next: Config.v2
}

contract Config.v2 {
  a: int
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "must be declared before") @>

    [<Fact>]
    let ``superseded generated type names cannot collide with declared contracts`` () =
        let diagnostics =
            resolveOne
                """
contract ConfigV1.v1 {
  a: int
}

contract Config.v1 {
  a: int
}

contract Config.v2 {
  a: int
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "collides") @>

    [<Fact>]
    let ``constraint and type mismatches are reported with guidance`` () =
        let diagnostics =
            resolveOne
                """
contract Broken.v1 {
  age: int [ pattern "x", min 3 ]
  name: text [ >= 1, distinct ]
  flag: bool [ max 4 ]
  password: text [ check entropyFloor ]
}
"""

        let all = messages diagnostics
        test <@ all |> List.exists (fun message -> message.Contains "'pattern' applies to text fields") @>
        test <@ all |> List.exists (fun message -> message.Contains "'min'/'max' bound the size") @>
        test <@ all |> List.exists (fun message -> message.Contains "'>=' applies to int or decimal fields") @>
        test <@ all |> List.exists (fun message -> message.Contains "'distinct' applies to list fields") @>
        test <@ all |> List.exists (fun message -> message.Contains "'check entropyFloor' references are not supported yet") @>

    [<Fact>]
    let ``defaults are type-checked and rejected on optional fields`` () =
        let diagnostics =
            resolveOne
                """
contract Broken.v1 {
  a: int = "nope"
  b?: text = "x"
  c: "on" | "off" = "auto"
}
"""

        let all = messages diagnostics
        test <@ all |> List.exists (fun message -> message.Contains "an int default must be a whole number") @>
        test <@ all |> List.exists (fun message -> message.Contains "optional fields cannot carry a default") @>
        test <@ all |> List.exists (fun message -> message.Contains "not one of the literal union cases") @>

    [<Fact>]
    let ``duplicate wire names fields and union tags are reported`` () =
        let diagnostics =
            resolveOne
                """
contract Geo.v1 {
  lat: decimal
}

contract Broken.v1 {
  a as "same": text
  b as "same": text
  b: int
  source: union kind {
    geo: Geo.v1
    geo: Geo.v1
  }
}
"""

        let all = messages diagnostics
        test <@ all |> List.exists (fun message -> message.Contains "duplicate wire name 'same'") @>
        test <@ all |> List.exists (fun message -> message.Contains "duplicate field name 'b'") @>
        test <@ all |> List.exists (fun message -> message.Contains "duplicate union case tag 'geo'") @>

    [<Fact>]
    let ``cross-file references resolve within one generation set`` () =
        let geo =
            parse "geo.contract"
                """
contract Geo.v1 {
  lat: decimal
}
"""

        let site =
            parse "site.contract"
                """
contract Site.v1 {
  location?: Geo.v1
}
"""

        test <@ Resolver.resolve [ geo; site ] = [] @>

    [<Fact>]
    let ``unknown annotations are reported`` () =
        let diagnostics =
            resolveOne
                """
contract Broken.v1 {
  @nonsense
  a: int
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "unknown annotation '@nonsense'") @>

    [<Fact>]
    let ``empty contracts are rejected before emission`` () =
        let diagnostics = resolveOne "contract Empty.v1 {\n}"
        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "at least one field") @>

    [<Fact>]
    let ``field names that collide after FSharp normalization are rejected`` () =
        let diagnostics =
            resolveOne
                """
contract Collision.v1 {
  foo: text
  Foo: text
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "generated field name 'Foo'") @>
        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "generated binding name 'foo'") @>

    [<Fact>]
    let ``identifiers that cannot safely name generated declarations are rejected`` () =
        let diagnostics =
            resolveOne
                """
contract type.v1 {
  _: text
}
"""

        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "contract name 'type'") @>
        test <@ messages diagnostics |> List.exists (fun message -> message.Contains "field name '_'") @>
