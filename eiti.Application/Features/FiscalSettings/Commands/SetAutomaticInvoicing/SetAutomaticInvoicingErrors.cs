using eiti.Application.Common;

namespace eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing;

public static class SetAutomaticInvoicingErrors
{
    public static readonly Error CompanyNotFound = Error.NotFound(
        "FiscalSettings.CompanyNotFound",
        "The current company was not found.");
}
