namespace eiti.Application.Features.Sales.Queries.GetSaleFiscalReceivers;

/// <param name="Available">False si el servicio fiscal no respondió: no se puede saber a quién se emitió.</param>
/// <param name="Invoice">Receptor de la factura que tuvo CAE (vigente o anulada). Null si no se facturó.</param>
/// <param name="Customer">Receptor que sale hoy de la ficha del cliente.</param>
/// <param name="CreditNote">Receptor de la última NC autorizada. Null si no hay.</param>
/// <param name="DebitNote">Receptor de la ND que neutralizó una NC mal emitida. Null si no hay.</param>
/// <param name="RegularizationStep">"none" | "debitNote" | "creditNote": qué falta para regularizar una NC
/// emitida a otro receptor que el de su factura.</param>
public sealed record GetSaleFiscalReceiversResponse(
    bool Available,
    string? UnavailableReason,
    SaleFiscalReceiverResponse? Invoice,
    SaleFiscalReceiverResponse Customer,
    bool CustomerMatchesInvoice,
    SaleFiscalReceiverResponse? CreditNote,
    bool CreditNoteMatchesInvoice,
    string RegularizationStep,
    SaleFiscalReceiverResponse? DebitNote);

/// <param name="Document">Comprobante, ej. "Factura A 00010-00000039". Null para la ficha del cliente.</param>
/// <param name="Identification">Ej. "CUIT: 30-68225650-4". Null para un consumidor final sin identificar.</param>
public sealed record SaleFiscalReceiverResponse(
    string? Document,
    string Name,
    string VatCondition,
    string? Identification);
