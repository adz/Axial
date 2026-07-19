/// The package's platform-variant surface. This file holds the package's FABLE_COMPILER directives (plus one guard
/// in Shape.fs excluding the quotation-based bare-getter `field` form, which Fable cannot interpret): each
/// half declares the same names, so the rest of the code is platform-directive-free. (Fable 5's project cracker does
/// not expose FABLE_COMPILER as an MSBuild property, so the variants live in one conditionally-halved file rather
/// than two conditionally-included ones; split them if Fable restores the property.)
module internal Axial.Schema.Platform

open System
open System.Collections.Generic

#if FABLE_COMPILER

/// Raises the richest argument-out-of-range failure the platform supports. Fable has no
/// ArgumentOutOfRangeException constructor carrying the offending value, so this lowers to invalidArg.
let argumentOutOfRange (parameterName: string) (value: obj) (message: string) : 'result =
    ignore value
    invalidArg parameterName message

/// Wraps mutable constraint arguments in a read-only view. Fable dictionaries satisfy the read-only interface
/// directly.
let freezeDictionary (values: Dictionary<string, obj>) : IReadOnlyDictionary<string, obj> =
    values :> IReadOnlyDictionary<string, obj>

/// Validates an underlying-primitive projection type eagerly. Fable erases generics at runtime, so the projection
/// type cannot be checked; the thunk is deliberately not invoked.
let checkUnderlyingProjection<'primitive> (getExpected: unit -> Type) (parameterName: string) : unit =
    ignore getExpected
    ignore parameterName

#else

/// Raises the richest argument-out-of-range failure the platform supports.
let argumentOutOfRange (parameterName: string) (value: obj) (message: string) : 'result =
    raise (ArgumentOutOfRangeException(parameterName, value, message))

/// Wraps mutable constraint arguments in a read-only view.
let freezeDictionary (values: Dictionary<string, obj>) : IReadOnlyDictionary<string, obj> =
    System.Collections.ObjectModel.ReadOnlyDictionary<string, obj>(values) :> IReadOnlyDictionary<string, obj>

/// Validates an underlying-primitive projection type eagerly. Fable erases generics at runtime, so this check is
/// .NET-only; the Fable variant ignores the thunk without invoking it.
let checkUnderlyingProjection<'primitive> (getExpected: unit -> Type) (parameterName: string) : unit =
    let expected = getExpected ()

    if typeof<'primitive> <> expected then
        invalidArg
            parameterName
            $"Expected the underlying primitive type {expected.Name}, but the requested projection type is {typeof<'primitive>.Name}."

#endif
