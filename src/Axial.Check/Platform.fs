/// The package's platform-variant surface. Fable's project cracker does not expose FABLE_COMPILER as an
/// MSBuild property, so the variants live in one conditionally-halved file rather than two
/// conditionally-included ones; split them if Fable restores the property.
module internal Axial.Check.Platform

open System

#if FABLE_COMPILER

/// Raises the richest argument-out-of-range failure the platform supports. Fable has no
/// ArgumentOutOfRangeException constructor carrying the offending value, so this lowers to invalidArg.
let argumentOutOfRange (parameterName: string) (value: obj) (message: string) : 'result =
    ignore value
    invalidArg parameterName message

#else

/// Raises the richest argument-out-of-range failure the platform supports.
let argumentOutOfRange (parameterName: string) (value: obj) (message: string) : 'result =
    raise (ArgumentOutOfRangeException(parameterName, value, message))

#endif
