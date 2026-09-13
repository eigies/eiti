using eiti.Domain.Companies;
using eiti.Domain.Sales;

namespace eiti.Application.Abstractions.Repositories;

public interface ISaleFiscalDocumentRepository
{
    Task AddAsync(SaleFiscalDocument document, CancellationToken cancellationToken = default);

    /// <summary>Todos los comprobantes de una venta: intentos, anulados y vigente.</summary>
    Task<IReadOnlyList<SaleFiscalDocument>> ListBySaleAsync(
        SaleId saleId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Por la clave de idempotencia. Respaldo del callback cuando todavía no se registró el id
    /// que asignó el servicio fiscal.
    /// </summary>
    Task<SaleFiscalDocument?> GetByRequestAsync(
        SaleId saleId,
        SaleFiscalDocumentKind kind,
        int sequence,
        CancellationToken cancellationToken = default);

    /// <summary>Por nuestro propio id, que es el requestId que ve el servicio fiscal.</summary>
    Task<SaleFiscalDocument?> GetByIdAsync(
        SaleFiscalDocumentId id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca por el id que asignó el servicio fiscal. Lo usa el callback, que no viene autenticado
    /// como usuario y por eso no puede scopear por empresa: ese id es un UUID que emitió el propio
    /// servicio y es la única clave que trae.
    /// </summary>
    Task<SaleFiscalDocument?> GetByFiscalDocumentIdAsync(
        Guid fiscalDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>Carga en lote para los listados, sin N+1 (mismo patrón que el transporte).</summary>
    Task<IReadOnlyList<SaleFiscalDocument>> ListBySaleIdsAsync(
        IReadOnlyCollection<SaleId> saleIds,
        CompanyId companyId,
        CancellationToken cancellationToken = default);
}
