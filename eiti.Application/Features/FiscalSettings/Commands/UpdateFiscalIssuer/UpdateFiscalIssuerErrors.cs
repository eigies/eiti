using eiti.Application.Common;

namespace eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;

public static class UpdateFiscalIssuerErrors
{
    public static readonly Error NotConfigured = Error.Conflict(
        "FiscalSettings.NotConfigured",
        "La facturación electrónica no está configurada para esta instalación.");

    public static Error NotSaved(string? reason) => Error.Conflict(
        "FiscalSettings.IssuerNotSaved",
        reason ?? "El servicio de facturación no guardó los datos.");
}
