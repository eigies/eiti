using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Users;
using MediatR;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eiti.Application.Features.Reports.Queries.ListAuditLog;

public sealed class ListAuditLogHandler : IRequestHandler<ListAuditLogQuery, Result<ListAuditLogResponse>>
{
    private const int MaxPageSize = 200;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISaleRepository _saleRepository;

    public ListAuditLogHandler(
        ICurrentUserService currentUserService,
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository,
        IPurchaseRepository purchaseRepository,
        ISaleRepository saleRepository)
    {
        _currentUserService = currentUserService;
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
        _purchaseRepository = purchaseRepository;
        _saleRepository = saleRepository;
    }

    public async Task<Result<ListAuditLogResponse>> Handle(ListAuditLogQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ListAuditLogResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = request.UserId.HasValue ? new UserId(request.UserId.Value) : null;

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 25 : Math.Min(request.PageSize, MaxPageSize);

        var totalCount = await _auditLogRepository.CountAsync(companyId, userId, from, to, cancellationToken);
        var entries = await _auditLogRepository.ListAsync(companyId, userId, from, to, page, pageSize, cancellationToken);

        var distinctUserIds = entries
            .Where(entry => entry.UserId is not null)
            .Select(entry => entry.UserId!.Value)
            .Distinct()
            .ToList();

        var usernamesById = distinctUserIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _userRepository.GetUsernamesByIdsAsync(distinctUserIds, cancellationToken);

        var purchaseIds = ExtractPurchaseIds(entries.Select(entry => entry.PayloadJson));
        var purchaseCodesById = purchaseIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _purchaseRepository.GetCodesByPurchaseIdsAsync(purchaseIds, cancellationToken);

        var saleIds = ExtractSaleIds(entries.Select(entry => (entry.ActionType, entry.PayloadJson)));
        var saleCodesById = saleIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken);

        var items = entries
            .Select(entry => new AuditLogItemResponse(
                entry.Id.Value,
                entry.UserId?.Value,
                entry.UserId is not null && usernamesById.TryGetValue(entry.UserId.Value, out var name) ? name : null,
                entry.ActionType,
                entry.Succeeded,
                entry.ErrorCode,
                NormalizePayload(entry.PayloadJson, entry.ActionType, purchaseCodesById, saleCodesById),
                entry.BeforeJson,
                entry.AfterJson,
                entry.Timestamp))
            .ToList();

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<ListAuditLogResponse>.Success(
            new ListAuditLogResponse(items, page, pageSize, totalCount, totalPages));
    }

    private static List<Guid> ExtractPurchaseIds(IEnumerable<string?> payloads)
    {
        var ids = new HashSet<Guid>();
        foreach (var payload in payloads)
        {
            try
            {
                CollectPurchaseIds(JsonNode.Parse(payload ?? string.Empty), ids);
            }
            catch
            {
                // El reporte debe seguir funcionando aunque un payload viejo no sea JSON valido.
            }
        }

        return ids.ToList();
    }

    private static List<Guid> ExtractSaleIds(IEnumerable<(string ActionType, string? PayloadJson)> entries)
    {
        var ids = new HashSet<Guid>();
        foreach (var (actionType, payloadJson) in entries)
        {
            try
            {
                CollectSaleIds(JsonNode.Parse(payloadJson ?? string.Empty), actionType, true, ids);
            }
            catch
            {
                // El reporte debe seguir funcionando aunque un payload viejo no sea JSON valido.
            }
        }

        return ids.ToList();
    }

    private static void CollectPurchaseIds(JsonNode? node, ISet<Guid> ids)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key == "PurchaseId"
                        && TryGetString(property.Value, out var value)
                        && Guid.TryParse(value, out var purchaseId))
                    {
                        ids.Add(purchaseId);
                    }

                    CollectPurchaseIds(property.Value, ids);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    CollectPurchaseIds(item, ids);
                }

                break;
        }
    }

    private static void CollectSaleIds(JsonNode? node, string actionType, bool isRoot, ISet<Guid> ids)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    var isSaleIdentifier = property.Key == "SaleId"
                        || (isRoot && property.Key == "Id" && UsesTopLevelSaleId(actionType));

                    if (isSaleIdentifier
                        && TryGetString(property.Value, out var value)
                        && Guid.TryParse(value, out var saleId))
                    {
                        ids.Add(saleId);
                    }

                    CollectSaleIds(property.Value, actionType, false, ids);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    CollectSaleIds(item, actionType, false, ids);
                }

                break;
        }
    }

    private static string? NormalizePayload(
        string? payloadJson,
        string actionType,
        IReadOnlyDictionary<Guid, string?> purchaseCodesById,
        IReadOnlyDictionary<Guid, string?> saleCodesById)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return payloadJson;

        try
        {
            var node = JsonNode.Parse(payloadJson);
            if (node is null)
                return payloadJson;

            var changed = NormalizeNode(node, actionType, true, purchaseCodesById, saleCodesById);
            return changed ? node.ToJsonString(PayloadJsonOptions) : payloadJson;
        }
        catch
        {
            return payloadJson;
        }
    }

    private static bool NormalizeNode(
        JsonNode node,
        string actionType,
        bool isRoot,
        IReadOnlyDictionary<Guid, string?> purchaseCodesById,
        IReadOnlyDictionary<Guid, string?> saleCodesById)
    {
        var changed = false;

        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Key == "RequiredPermissions")
                    {
                        obj.Remove(property.Key);
                        changed = true;
                        continue;
                    }

                    var isSaleIdentifier = property.Key == "SaleId"
                        || (isRoot && property.Key == "Id" && UsesTopLevelSaleId(actionType));

                    if (property.Key == "PurchaseId"
                        && TryGetString(property.Value, out var value)
                        && Guid.TryParse(value, out var purchaseId)
                        && purchaseCodesById.TryGetValue(purchaseId, out var code)
                        && !string.IsNullOrWhiteSpace(code))
                    {
                        obj[property.Key] = code;
                        changed = true;
                        continue;
                    }

                    if (isSaleIdentifier
                        && TryGetString(property.Value, out var saleValue)
                        && Guid.TryParse(saleValue, out var saleId)
                        && saleCodesById.TryGetValue(saleId, out var saleCode)
                        && !string.IsNullOrWhiteSpace(saleCode))
                    {
                        obj[property.Key] = saleCode;
                        changed = true;
                        continue;
                    }

                    if (property.Value is not null)
                    {
                        changed |= NormalizeNode(property.Value, actionType, false, purchaseCodesById, saleCodesById);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        changed |= NormalizeNode(item, actionType, false, purchaseCodesById, saleCodesById);
                    }
                }

                break;
        }

        return changed;
    }

    private static bool UsesTopLevelSaleId(string actionType) =>
        actionType is "UpdateSaleCommand"
            or "DeleteSaleCommand"
            or "CancelSaleCommand"
            or "SendSaleWhatsAppCommand";

    private static bool TryGetString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is null)
            return false;

        try
        {
            value = node.GetValue<string>();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
