namespace Axial.Tests

open System
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module RulesTests =
    type private SupportTicket =
        {
            Priority: int
            HasAssignee: bool
        }

    type private TicketRuleError =
        | HighPriorityNeedsAssignee
        | ManualReviewRequired

    let private needsAssignee ticket =
        if ticket.Priority >= 4 && not ticket.HasAssignee then
            Error(Diagnostics.singleton HighPriorityNeedsAssignee)
        else
            Ok ()

    let private needsReview ticket =
        if ticket.Priority >= 5 then
            Error(Diagnostics.singleton ManualReviewRequired)
        else
            Ok ()

    [<Fact>]
    let ``explicit Rules API creates an empty contextual rule set`` () =
        let ruleSet = Rules.empty<SupportTicket, TicketRuleError>

        test <@ ruleSet.GetType().Name.StartsWith("RuleSet") @>

    [<Fact>]
    let ``explicit Rules API creates and composes contextual rule sets`` () =
        let ruleSet =
            Rules.concat
                [ Rules.create needsAssignee
                  Rules.ofList [ needsReview ] ]

        test <@ ruleSet.GetType().Name.StartsWith("RuleSet") @>

    [<Fact>]
    let ``explicit Rules API rejects null rule functions`` () =
        let ex =
            Assert.Throws<ArgumentNullException>(fun () ->
                Rules.create (Unchecked.defaultof<SupportTicket -> Result<unit, Diagnostics<TicketRuleError>>>)
                |> ignore)

        test <@ ex.ParamName = "rule" @>
