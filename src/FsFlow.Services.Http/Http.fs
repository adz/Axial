namespace FsFlow.Services.Http

open System.Net.Http
open System.Threading.Tasks
open FsFlow

/// <summary>Provides asynchronous access to HTTP client operations.</summary>
type IHttp =
    /// <summary>Sends a GET request to the specified URL and returns the response body as a string.</summary>
    abstract GetString : url: string -> Task<string>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Http =
    /// <summary>Sends a GET request through an explicit HTTP service and returns the response body.</summary>
    let getString<'env, 'error when 'env :> IHas<IHttp>>
        (url: string)
        : Flow<'env, 'error, string> =
        flow {
            let! http = Service<IHttp>.get()
            return! http.GetString(url)
        }

#if !FABLE_COMPILER
    /// <summary>Creates a live HTTP service backed by <see cref="T:System.Net.Http.HttpClient" />.</summary>
    let live (client: HttpClient) : IHttp =
        { new IHttp with
            member _.GetString(url) = client.GetStringAsync(url) }

    /// <summary>Builds a live HTTP service as a layer.</summary>
    let layer (client: HttpClient) : Layer<unit, Never, IHttp> =
        Layer.succeed (live client)
#endif
