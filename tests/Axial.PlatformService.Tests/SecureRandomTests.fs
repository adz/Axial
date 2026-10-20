namespace Axial.Tests

open Axial
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module SecureRandomTests =
    type private SecureEnv =
        { SecureRandomService: ISecureRandom }
        interface IHasSecureRandom with
            member this.SecureRandom = this.SecureRandomService

    [<Fact>]
    let ``deterministic secure random cycles through supplied bytes`` () =
        let environment = { SecureRandomService = SecureRandom.fromBytes [ 1uy; 2uy; 3uy ] }

        let result =
            SecureRandom.bytes 8
            |> Flow.runSync environment

        test <@ result = Exit.Success [| 1uy; 2uy; 3uy; 1uy; 2uy; 3uy; 1uy; 2uy |] @>

    [<Fact>]
    let ``deterministic secure random integers stay in the requested range`` () =
        let environment = { SecureRandomService = SecureRandom.fromBytes [ 0uy; 127uy; 255uy ] }

        let values =
            [ for _ in 1..100 ->
                SecureRandom.int32 -20 30
                |> Flow.runSync environment ]

        test <@ values |> List.forall (function Exit.Success value -> value >= -20 && value < 30 | _ -> false) @>

    [<Fact>]
    let ``live secure random fills a buffer with nonzero data`` () =
        let environment = { SecureRandomService = SecureRandom.live }

        let result =
            SecureRandom.bytes 32
            |> Flow.runSync environment

        test <@ result |> function Exit.Success bytes -> bytes |> Array.exists ((<>) 0uy) | _ -> false @>
