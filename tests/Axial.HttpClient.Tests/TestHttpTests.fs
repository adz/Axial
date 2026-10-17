namespace Axial.Tests

open System
open System.Threading
open Axial
open Axial.HttpClient
open Swensen.Unquote
open Xunit

module TestHttpTests =
    [<Fact>]
    let ``test http records requests and replies through the handler`` () =
        let http = Http.test ()
        let startedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        http.Handler <- fun _ -> Ok(Response.create startedAt 201 "created")

        let response =
            match (http :> IHttp).Send(Http.get "https://example.test/items", CancellationToken.None) |> Async.RunSynchronously with
            | Ok response -> response
            | Error error -> failwithf "Expected Ok, got %A" error

        test <@ response.StatusCode = 201 @>
        test <@ response.Text = "created" @>
        test <@ http.Sent.Count = 1 @>
        test <@ Request.url http.Sent[0] = "https://example.test/items" @>

    [<Fact>]
    let ``test http can reply with a scripted error`` () =
        let http = Http.test ()
        http.Handler <- fun _ -> Error(HttpError.ConnectionFailed("https://example.test", "down"))

        let result = (http :> IHttp).Send(Http.get "https://example.test", CancellationToken.None) |> Async.RunSynchronously

        test <@ match result with Error(HttpError.ConnectionFailed _) -> true | _ -> false @>
