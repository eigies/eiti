using eiti.Application.Abstractions.Services;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;

/// <summary>Textos del comprobante impreso. Las descripciones de condición de IVA son las oficiales de ARCA.</summary>
internal static class FiscalPrintLabels
{
    /// <summary>Letra, código ARCA, título y nombre corto del tipo que devuelve el servicio (p. ej. "invoiceB").</summary>
    public static (string Letter, int Code, string Title, string Name)? DescribeType(string? type) => type switch
    {
        "invoiceA" => ("A", 1, "FACTURA", "Factura A"),
        "debitNoteA" => ("A", 2, "NOTA DE DÉBITO", "Nota de débito A"),
        "creditNoteA" => ("A", 3, "NOTA DE CRÉDITO", "Nota de crédito A"),
        "invoiceB" => ("B", 6, "FACTURA", "Factura B"),
        "debitNoteB" => ("B", 7, "NOTA DE DÉBITO", "Nota de débito B"),
        "creditNoteB" => ("B", 8, "NOTA DE CRÉDITO", "Nota de crédito B"),
        _ => null
    };

    public static string VatCondition(FiscalReceiverVatCondition condition) => condition switch
    {
        FiscalReceiverVatCondition.Registered => "IVA Responsable Inscripto",
        FiscalReceiverVatCondition.Monotribute => "Responsable Monotributo",
        FiscalReceiverVatCondition.Exempt => "IVA Sujeto Exento",
        _ => "Consumidor Final"
    };

    public static string? Identification(FiscalReceiver? receiver) => receiver?.DocType?.ToUpperInvariant() switch
    {
        "CUIT" when !string.IsNullOrWhiteSpace(receiver.DocNumber) => $"CUIT: {FormatCuit(receiver.DocNumber)}",
        "DNI" when !string.IsNullOrWhiteSpace(receiver.DocNumber) => $"DNI: {receiver.DocNumber}",
        _ => null
    };

    public static string FormatCuit(string cuit) =>
        cuit.Length == 11 ? $"{cuit[..2]}-{cuit[2..10]}-{cuit[10]}" : cuit;
}
