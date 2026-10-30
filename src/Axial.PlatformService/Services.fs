namespace Axial.PlatformService

open System.Collections.Generic

/// Environment-variable access is an application capability, not a runtime platform value.
type IEnvironmentVariables =
    abstract TryGet: name: string -> string option
    abstract Set: name: string * value: string option -> unit
    abstract Expand: text: string -> string
    abstract GetAll: unit -> IReadOnlyDictionary<string, string>

#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasEnvironmentVariables =
    abstract EnvironmentVariables: IEnvironmentVariables

[<RequireQualifiedAccess>]
type EnvironmentVariableError =
    | MissingVariable of name: string
    | InvalidVariable of name: string * value: string * expected: string
    override this.ToString() =
        match this with
        | MissingVariable name -> System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Missing required environment variable '{0}'.", name)
        | InvalidVariable(name, value, expected) -> System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Environment variable '{0}' had value '{1}' but expected {2}.", name, value, expected)
