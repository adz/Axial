namespace Axial.Validation.Schema

open Axial.Validation

/// <summary>
/// A collection of contextual rules evaluated over an already-trusted model.
/// </summary>
/// <remarks>
/// <para>
/// Schema constraints describe field-local value requirements that can run during input parsing or intrinsic model
/// validation. A <c>RuleSet</c> is reserved for contextual requirements that need the completed model and may attach
/// failures anywhere in a diagnostics path.
/// </para>
/// <para>
/// Rule sets do not construct models. They evaluate the supplied model and either accept it or return path-aware
/// diagnostics.
/// </para>
/// </remarks>
type RuleSet<'model, 'error> =
    internal
        {
            Rules: ('model -> Result<unit, Diagnostics<'error>>) list
        }
