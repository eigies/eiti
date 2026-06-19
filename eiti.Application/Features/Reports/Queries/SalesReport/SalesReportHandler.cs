using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Transport;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.SalesReport;

public sealed class SalesReportHandler : IRequestHandler<SalesReportQuery, Result<SalesReportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISaleTransportAssignmentRepository _transportRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public SalesReportHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ISaleTransportAssignmentRepository transportRepository,
        IEmployeeRepository employeeRepository,
        IVehicleRepository vehicleRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _transportRepository = transportRepository;
        _employeeRepository = employeeRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<SalesReportResponse>> Handle(SalesReportQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<SalesReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var groupBy = request.GroupBy.ToLowerInvariant();

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var sales = (await _saleRepository.ListByCompanyAsync(companyId, from, to, null, includeCuentaCorriente: true, cancellationToken))
            .Where(s => s.SaleStatus != SaleStatus.Cancel)
            .ToList();

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            sales = sales.Where(s => allowed.Contains(s.BranchId.Value)).ToList();
        }

        // Filtros a nivel venta
        if (request.BranchId.HasValue)
            sales = sales.Where(s => s.BranchId.Value == request.BranchId.Value).ToList();

        if (request.CustomerId.HasValue)
            sales = sales.Where(s => s.CustomerId is not null && s.CustomerId.Value == request.CustomerId.Value).ToList();

        if (request.Channel.HasValue)
            sales = sales.Where(s => s.SourceChannel is not null && (int)s.SourceChannel.Value == request.Channel.Value).ToList();

        if (!string.IsNullOrWhiteSpace(request.DeliveryMode))
        {
            sales = request.DeliveryMode.ToLowerInvariant() switch
            {
                "with" => sales.Where(s => s.HasDelivery).ToList(),
                "without" => sales.Where(s => !s.HasDelivery).ToList(),
                _ => sales
            };
        }

        // Mayorista = ventas por Cuenta Corriente; Minorista = ventas normales.
        if (!string.IsNullOrWhiteSpace(request.SaleType))
        {
            sales = request.SaleType.ToLowerInvariant() switch
            {
                "wholesale" => sales.Where(s => s.IsCuentaCorriente).ToList(),
                "retail" => sales.Where(s => !s.IsCuentaCorriente).ToList(),
                _ => sales
            };
        }

        // Asignaciones de transporte (instalador/vehículo) mapeadas por la asignación vigente de cada venta
        var saleIds = sales.Select(s => s.Id).ToList();
        var assignments = saleIds.Count == 0
            ? Array.Empty<SaleTransportAssignment>()
            : (await _transportRepository.ListBySaleIdsAsync(saleIds, companyId, cancellationToken)).ToArray();
        var assignmentById = assignments.ToDictionary(a => a.Id.Value);

        SaleTransportAssignment? AssignmentOf(Sale sale) =>
            sale.TransportAssignmentId is not null && assignmentById.TryGetValue(sale.TransportAssignmentId.Value, out var a)
                ? a
                : null;

        if (request.InstallerId.HasValue)
            sales = sales.Where(s => AssignmentOf(s)?.DriverEmployeeId.Value == request.InstallerId.Value).ToList();

        if (request.VehicleId.HasValue)
            sales = sales.Where(s => AssignmentOf(s)?.VehicleId.Value == request.VehicleId.Value).ToList();

        // Diccionarios de apoyo
        var products = (await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(p => p.Id.Value, p => (p.Brand, p.Name, p.CostPrice, p.CategoryId));

        var employees = new Dictionary<Guid, string>();
        var vehicles = new Dictionary<Guid, string>();
        if (groupBy is "installer" or "vehicle")
        {
            foreach (var e in await _employeeRepository.ListByCompanyAsync(companyId, cancellationToken))
                employees[e.Id.Value] = e.FullName;
            foreach (var v in await _vehicleRepository.ListByCompanyAsync(companyId, cancellationToken))
                vehicles[v.Id.Value] = string.IsNullOrWhiteSpace(v.Model) ? v.Plate : $"{v.Plate} · {v.Model}";
        }

        // Agregación por líneas
        var groups = new Dictionary<string, Accumulator>();
        foreach (var sale in sales)
        {
            var assignment = AssignmentOf(sale);
            foreach (var line in sale.Details)
            {
                products.TryGetValue(line.ProductId.Value, out var prod);

                // Filtro por categoría: saltea las líneas cuyo producto no pertenece a la categoría pedida.
                if (request.CategoryId.HasValue && prod.CategoryId != request.CategoryId.Value)
                    continue;

                var revenue = line.TotalAmount;
                var unitCost = line.UnitCost ?? prod.CostPrice;
                var cost = decimal.Round(line.Quantity * unitCost, 2, MidpointRounding.AwayFromZero);

                var (key, label, subKey, subLabel) = ResolveDimension(groupBy, sale, line, prod, assignment, employees, vehicles);
                var dictKey = subKey is null ? key : $"{key}||{subKey}";

                if (!groups.TryGetValue(dictKey, out var acc))
                {
                    acc = new Accumulator(key, label, subKey, subLabel);
                    groups[dictKey] = acc;
                }

                acc.SaleIds.Add(sale.Id.Value);
                acc.Units += line.Quantity;
                acc.Revenue += revenue;
                acc.Cost += cost;
            }
        }

        var rows = groups.Values
            .Select(a => new SalesReportRow(
                a.Key, a.Label, a.SubKey, a.SubLabel,
                a.SaleIds.Count, a.Units, a.Revenue, a.Cost, a.Revenue - a.Cost,
                Margin(a.Revenue, a.Revenue - a.Cost)))
            .ToList();

        rows = groupBy == "product_ranking"
            ? rows.OrderByDescending(r => r.Units).ThenByDescending(r => r.Revenue).ToList()
            : rows.OrderByDescending(r => r.Revenue).ToList();

        // Tickets distintos que realmente aportaron líneas (correcto también con el filtro de categoría activo).
        var totalSaleIds = groups.Values.SelectMany(a => a.SaleIds).Distinct().Count();
        var totalUnits = rows.Sum(r => r.Units);
        var totalRevenue = rows.Sum(r => r.Revenue);
        var totalCost = rows.Sum(r => r.Cost);
        var totals = new SalesReportTotals(
            totalSaleIds, totalUnits, totalRevenue, totalCost, totalRevenue - totalCost,
            Margin(totalRevenue, totalRevenue - totalCost));

        return Result<SalesReportResponse>.Success(new SalesReportResponse(groupBy, rows, totals));
    }

    private static (string Key, string Label, string? SubKey, string? SubLabel) ResolveDimension(
        string groupBy,
        Sale sale,
        SaleDetail line,
        (string Brand, string Name, decimal CostPrice, Guid? CategoryId) prod,
        SaleTransportAssignment? assignment,
        IReadOnlyDictionary<Guid, string> employees,
        IReadOnlyDictionary<Guid, string> vehicles)
    {
        switch (groupBy)
        {
            case "brand":
                return (NullSafe(prod.Brand), NullSafe(prod.Brand), null, null);

            case "channel":
                return (ChannelKey(sale), ChannelLabel(sale), null, null);

            case "channel_brand":
                return (ChannelKey(sale), ChannelLabel(sale), NullSafe(prod.Brand), NullSafe(prod.Brand));

            case "installer":
            {
                var id = assignment?.DriverEmployeeId.Value;
                if (id is null) return ("none", "(Sin asignar)", null, null);
                return (id.Value.ToString(), employees.TryGetValue(id.Value, out var n) ? n : "(Empleado)", null, null);
            }

            case "vehicle":
            {
                var id = assignment?.VehicleId.Value;
                if (id is null) return ("none", "(Sin asignar)", null, null);
                return (id.Value.ToString(), vehicles.TryGetValue(id.Value, out var n) ? n : "(Vehículo)", null, null);
            }

            case "total":
                return ("total", "Total", null, null);

            // product / product_ranking
            default:
                return (line.ProductId.Value.ToString(),
                    string.IsNullOrWhiteSpace(prod.Name) ? "(Producto)" : $"{prod.Brand} {prod.Name}".Trim(),
                    null, null);
        }
    }

    private static string ChannelKey(Sale sale) =>
        sale.SourceChannel is null ? "0" : ((int)sale.SourceChannel.Value).ToString();

    private static string ChannelLabel(Sale sale) => sale.SourceChannel switch
    {
        SaleSourceChannel.Referral => "Referido",
        SaleSourceChannel.WhatsApp => "WhatsApp",
        SaleSourceChannel.Facebook => "Facebook",
        SaleSourceChannel.Web => "Web",
        SaleSourceChannel.Instagram => "Instagram",
        SaleSourceChannel.Other => "Otro",
        SaleSourceChannel.PreviousCustomer => "Cliente anterior",
        SaleSourceChannel.MercadoLibre => "MercadoLibre",
        SaleSourceChannel.Google => "Google",
        SaleSourceChannel.NoChannel => "Sin canal",
        _ => "Sin canal"
    };

    private static string NullSafe(string? value) => string.IsNullOrWhiteSpace(value) ? "(Sin dato)" : value;

    private static decimal Margin(decimal revenue, decimal profit) =>
        revenue == 0 ? 0 : decimal.Round(profit / revenue * 100, 2, MidpointRounding.AwayFromZero);

    private sealed class Accumulator
    {
        public Accumulator(string key, string label, string? subKey, string? subLabel)
        {
            Key = key;
            Label = label;
            SubKey = subKey;
            SubLabel = subLabel;
        }

        public string Key { get; }
        public string Label { get; }
        public string? SubKey { get; }
        public string? SubLabel { get; }
        public HashSet<Guid> SaleIds { get; } = new();
        public int Units { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
    }
}
