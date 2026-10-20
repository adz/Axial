module Axial.Fable.Tests.SecureRandomTests

open Axial
open Axial.Fable.Tests.Harness
open Axial.PlatformService

let tests =
    [ test "live secure random fills buffers larger than the Web Crypto call limit" <|
        flow {
            let buffer = Array.zeroCreate<byte> 65_537
            SecureRandom.live.Fill buffer

            return
                [ equal "length" 65_537 buffer.Length
                  isTrue "filled" (buffer |> Array.exists ((<>) 0uy)) ]
        } ]
