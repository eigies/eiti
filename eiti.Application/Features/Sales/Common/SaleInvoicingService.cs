using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Traduce una venta al pedido de comprobante y aplica el resultado sobre el
/// <see cref="SaleFiscalDocument"/> correspondiente. La venta nunca se toca: facturar es un hecho
/// aparte que ocurre *sobre* una venta, no un atributo de ella.
/// </summary>
public interface ISaleInvoicingService
{
    bool IsEnabled { get; }

    /// <summary>Config efectiva de facturación automática: sucursal ?? empresa.</summary>
    Task<bool> IsAutomaticAsync(CompanyId companyId, BranchId branchId, CancellationToken cancellationToken);

    /// <summary>Emite (o reintenta) la factura de la venta. No lanza: informa por el resultado.</summary>
    Task<SaleInvoicingOutcome> InvoiceAsync(Sale sale, CancellationToken cancellationToken);

    /// <summary>Emite la nota de crédito que anula la factura de la venta.</summary>
    Task<SaleInvoicingOutcome> IssueCreditNoteAsync(Sale sale, CancellationToken cancellationToken);
}

public sealed record SaleInvoicingOutcome(
    bool IsSuccess,
    SaleInvoicingStatus Status,
    SaleFiscalDocument? Document = null,
    string? Message = null);

public sealed class SaleInvoicingService : ISaleInvoicingService
{
    // Alícuota asumida cuando la venta no trae IVA discriminado. El emisor es Responsable
    // Inscripto: el total siempre lleva IVA contenido, aunque la venta no lo haya desglosado.
    private const decimal DefaultVatRate = 21m;

    private readonly IFiscalizationService _fiscalizationService;
    private readonly ISaleFiscalDocumentRepository _fiscalDocuments;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;

    public SaleInvoicingService(
        IFiscalizationService fiscalizationService,
        ISaleFiscalDocumentRepository fiscalDocuments,
        ICustomerRepository customerRepository,
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository)
    {
        _fiscalizationService = fiscalizationService;
        _fiscalDocuments = fiscalDocuments;
        _customerRepository = customerRepository;
        _companyRepository = companyRepository;
        _branchRepository = branchRepository;
    }

    public bool IsEnabled => _fiscalizationService.IsEnabled;

    public async Task<bool> IsAutomaticAsync(CompanyId companyId, BranchId branchId, CancellationToken cancellationToken)
    {
        if (!_fiscalizationService.IsEnabled)
        {
            return false;
        }

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
        {
            return false;
        }

        var branch = await _branchRepository.GetByIdAsync(branchId, companyId, cancellationToken);
        return branch?.ResolveAutomaticInvoicing(company.AutomaticInvoicing) ?? company.AutomaticInvoicing;
    }

    public async Task<SaleInvoicingOutcome> InvoiceAsync(Sale sale, CancellationToken cancellationToken)
    {
        if (!_fiscalizationService.IsEnabled)
        {
            return new SaleInvoicingOutcome(false, SaleInvoicingStatus.NotInvoiced,
                Message: "El servicio de facturación no está configurado.");
        }

        var documents = await _fiscalDocuments.ListBySaleAsync(sale.Id, sale.CompanyId, cancellationToken);

        // Si ya hay un intento en vuelo se RE-ENVÍA ese mismo, con su RequestId: si el servicio
        // llegó a emitirlo y se perdió la respuesta, lo devuelve en vez de emitir un segundo
        // comprobante. Solo se abre un intento nuevo cuando el anterior quedó rechazado.
        var document = SaleFiscalDocumentRules.InFlight(documents, SaleFiscalDocumentKind.Invoice);
        var customer = await LoadCustomerAsync(sale, cancellationToken);

        if (document is null)
        {
            document = SaleFiscalDocument.CreateInvoice(
                sale.CompanyId,
                sale.Id,
                SaleFiscalDocumentRules.NextSequence(documents, SaleFiscalDocumentKind.Invoice));
            await _fiscalDocuments.AddAsync(document, cancellationToken);

            // Solo se valida al abrir un intento NUEVO. Un intento en vuelo pudo haberse emitido:
            // ese se re-envía siempre, para que el servicio devuelva lo que ya autorizó.
            // El intento queda Rechazado con el motivo, igual que un rechazo del fisco: el
            // usuario lo ve en la venta, completa el dato y reintenta.
            var receiverError = SaleInvoicingReceiverRules.Validate(customer);
            if (receiverError is not null)
            {
                document.Reject(null, receiverError);
                return new SaleInvoicingOutcome(false, SaleInvoicingStatus.Rejected, document, receiverError);
            }
        }

        var request = new FiscalDocumentRequest(
            RequestId: document.RequestId,
            TenantId: sale.CompanyId.Value,
            RequestedType: FiscalRequestedDocumentType.Auto,
            Receiver: BuildReceiver(customer),
            Amounts: BuildAmounts(sale),
            Date: DateOnly.FromDateTime(sale.CreatedAt));

        var result = await _fiscalizationService.RequestDocumentAsync(request, cancellationToken);
        return Apply(document, result);
    }

    public async Task<SaleInvoicingOutcome> IssueCreditNoteAsync(Sale sale, CancellationToken cancellationToken)
    {
        if (!_fiscalizationService.IsEnabled)
        {
            return new SaleInvoicingOutcome(false, SaleInvoicingStatus.NotInvoiced,
                Message: "El servicio de facturación no está configurado.");
        }

        var documents = await _fiscalDocuments.ListBySaleAsync(sale.Id, sale.CompanyId, cancellationToken);
        var invoice = SaleInvoicingView.From(documents).LiveInvoice;

        if (invoice is null || invoice.Number is null || invoice.PointOfSale is null || invoice.DocumentType is null)
        {
            return new SaleInvoicingOutcome(false, SaleInvoicingStatus.NotInvoiced,
                Message: "La venta no tiene una factura vigente a la que asociar la nota de crédito.");
        }

        var creditNote = SaleFiscalDocumentRules.InFlight(documents, SaleFiscalDocumentKind.CreditNote);

        if (creditNote is null)
        {
            // La NC referencia la FACTURA, no la venta: es lo que el fisco asocia.
            creditNote = SaleFiscalDocument.CreateCreditNote(
                sale.CompanyId,
                sale.Id,
                invoice,
                SaleFiscalDocumentRules.NextSequence(documents, SaleFiscalDocumentKind.CreditNote));
            await _fiscalDocuments.AddAsync(creditNote, cancellationToken);
        }

        var request = new FiscalDocumentRequest(
            RequestId: creditNote.RequestId,
            TenantId: sale.CompanyId.Value,
            RequestedType: FiscalRequestedDocumentType.CreditNote,
            Receiver: BuildReceiver(await LoadCustomerAsync(sale, cancellationToken)),
            Amounts: BuildAmounts(sale),
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            PointOfSale: invoice.PointOfSale,
            AssociatedDocument: new FiscalAssociatedDocument(
                invoice.DocumentType,
                invoice.PointOfSale.Value,
                invoice.Number.Value,
                DateOnly.FromDateTime(invoice.IssuedAt ?? sale.CreatedAt)));

        var result = await _fiscalizationService.RequestDocumentAsync(request, cancellationToken);
        var outcome = Apply(creditNote, result);

        // Si la NC quedó autorizada acá mismo, la factura se anula ya. Si quedó encolada, lo hace
        // el callback — por el mismo camino.
        await SaleFiscalDocumentEffects.ApplyAsync(creditNote, _fiscalDocuments, cancellationToken);

        return outcome;
    }

    private static SaleInvoicingOutcome Apply(SaleFiscalDocument document, FiscalDocumentResult result)
    {
        switch (result.Outcome)
        {
            case FiscalDocumentOutcome.Authorized when result.DocumentId is not null:
                document.Authorize(
                    result.DocumentId.Value,
                    result.DocumentType,
                    result.PointOfSale,
                    result.Number,
                    result.AuthorizationCode,
                    result.ValidUntil,
                    result.QrUrl,
                    DateTime.UtcNow);
                return new SaleInvoicingOutcome(true, SaleInvoicingStatus.Invoiced, document);

            case FiscalDocumentOutcome.Queued when result.DocumentId is not null:
                document.Queue(result.DocumentId.Value);
                return new SaleInvoicingOutcome(true, SaleInvoicingStatus.InProgress, document);

            case FiscalDocumentOutcome.Rejected:
                // El fisco rechazó por datos: se sabe que NO se emitió nada. El reintento puede
                // arrancar una secuencia nueva sin riesgo de duplicar.
                document.Reject(result.DocumentId, result.ErrorMessage);
                return new SaleInvoicingOutcome(false, SaleInvoicingStatus.Rejected, document, result.ErrorMessage);

            default:
                // Resultado DESCONOCIDO (timeout, caída, respuesta perdida): el comprobante pudo
                // haberse emitido. Queda en trámite para que el reintento re-envíe el MISMO
                // RequestId y el servicio devuelva el que ya emitió.
                //
                // No se intenta distinguir "no llegó a salir" de "salió y se perdió la respuesta":
                // el riesgo es asimétrico. Equivocarse hacia trámite cuesta un reintento; hacia
                // rechazo cuesta un comprobante fiscal duplicado, que solo se arregla con otra
                // nota de crédito.
                document.MarkUnconfirmed(result.ErrorMessage);
                return new SaleInvoicingOutcome(false, SaleInvoicingStatus.InProgress, document, result.ErrorMessage);
        }
    }

    /// <summary>
    /// El total de la venta ya incluye el IVA. El neto se obtiene restando el IVA contenido.
    /// El recargo por tarjeta queda FUERA a propósito: no está en TotalAmount y su tratamiento
    /// fiscal todavía no está definido por el contador.
    /// </summary>
    private static FiscalAmounts BuildAmounts(Sale sale)
    {
        var total = decimal.Round(sale.TotalAmount, 2, MidpointRounding.AwayFromZero);
        var rate = sale.VatRate ?? DefaultVatRate;

        if (rate <= 0m)
        {
            return new FiscalAmounts(total, [new FiscalVatAmount(0m, total, 0m)], 0m, total);
        }

        var vatAmount = sale.VatAmount is not null
            ? decimal.Round(sale.VatAmount.Value, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(total - total / (1m + rate / 100m), 2, MidpointRounding.AwayFromZero);
        var net = decimal.Round(total - vatAmount, 2, MidpointRounding.AwayFromZero);

        return new FiscalAmounts(net, [new FiscalVatAmount(rate, net, vatAmount)], 0m, total);
    }

    private async Task<Customer?> LoadCustomerAsync(Sale sale, CancellationToken cancellationToken) =>
        sale.CustomerId is null
            ? null
            : await _customerRepository.GetByIdAsync(sale.CustomerId, sale.CompanyId, cancellationToken);

    /// <summary>
    /// Sin cliente devuelve null: el servicio lo toma como Consumidor Final sin identificar
    /// (Factura B, DocTipo 99). Es la venta de mostrador y es el caso más común.
    /// </summary>
    private static FiscalReceiver? BuildReceiver(Customer? customer)
    {
        if (customer is null)
        {
            return null;
        }

        var vatCondition = customer.IvaCondition switch
        {
            IvaCondition.ResponsableInscripto => FiscalReceiverVatCondition.Registered,
            IvaCondition.Monotributo => FiscalReceiverVatCondition.Monotribute,
            IvaCondition.Exento => FiscalReceiverVatCondition.Exempt,
            _ => FiscalReceiverVatCondition.FinalConsumer
        };

        // Para Factura A el fisco exige CUIT; para el resto alcanza el documento, si lo hay.
        var needsTaxId = vatCondition is FiscalReceiverVatCondition.Registered or FiscalReceiverVatCondition.Monotribute;
        var taxId = OnlyDigits(customer.TaxId);

        if (needsTaxId || (!string.IsNullOrWhiteSpace(taxId) && customer.DocumentNumber is null))
        {
            return new FiscalReceiver(vatCondition, taxId is null ? null : "CUIT", taxId, customer.FullName);
        }

        var documentNumber = OnlyDigits(customer.DocumentNumber);
        var documentType = customer.DocumentType == DocumentType.Dni && documentNumber is not null ? "DNI" : null;

        return new FiscalReceiver(vatCondition, documentType, documentType is null ? null : documentNumber, customer.FullName);
    }

    private static string? OnlyDigits(string? value) => SaleInvoicingReceiverRules.OnlyDigits(value);
}
