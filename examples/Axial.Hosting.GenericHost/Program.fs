open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Axial.Flow
open Axial.Flow.Hosting

type IMessageSource =
    abstract Message: string

type MessageSource() =
    interface IMessageSource with
        member _.Message = "Generic Host supplied this dependency."

type AppEnv =
    { Messages: IMessageSource
      Logger: ILogger }

type AppError =
    | ApplicationFailure of string

let describeError = function
    | ApplicationFailure message -> message

let application : Flow<AppEnv, AppError, unit> =
    flow {
        let! environment = Flow.env
        // StopAsync waits for root finalizers, so host shutdown cannot overtake application cleanup.
        do! Flow.addFinalizerAsync (fun _ -> async { environment.Logger.LogInformation("Root cleanup finished") })
        do! async { environment.Logger.LogInformation("{Message}", environment.Messages.Message) }
        do! Flow.Runtime.sleep(TimeSpan.FromDays 1.0)
    }

[<EntryPoint>]
let main arguments =
    let builder = Host.CreateApplicationBuilder arguments
    builder.Services.AddSingleton<IMessageSource, MessageSource>() |> ignore

    // Resolve framework services once at the edge; workflows receive an explicit application environment.
    builder.Services
    |> Hosting.addApp
        (fun services ->
            { Messages = services.GetRequiredService<IMessageSource>()
              Logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Example.Application") })
        describeError
        application
    |> ignore

    builder.Build().Run()
    0
