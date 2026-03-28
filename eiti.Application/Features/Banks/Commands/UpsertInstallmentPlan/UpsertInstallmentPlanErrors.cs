using eiti.Application.Common;

namespace eiti.Application.Features.Banks.Commands.UpsertInstallmentPlan;

public static class UpsertInstallmentPlanErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Banks.UpsertPlan.NotFound",
        "The bank was not found.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Banks.UpsertPlan.Unauthorized",
        "Authentication is required.");

    public static readonly Error InvalidCuotas = Error.Validation(
        "Banks.UpsertPlan.InvalidCuotas",
        "The cuotas value is invalid. Valid values are: 1, 3, 6, 9, 12.");
}
