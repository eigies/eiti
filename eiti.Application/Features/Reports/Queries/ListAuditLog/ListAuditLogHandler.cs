using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.ListAuditLog;

public sealed class ListAuditLogHandler : IRequestHandler<ListAuditLogQuery, Result<ListAuditLogResponse>>
{
    private const int MaxPageSize = 200;

    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public ListAuditLogHandler(
        ICurrentUserService currentUserService,
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
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

        var items = entries
            .Select(entry => new AuditLogItemResponse(
                entry.Id.Value,
                entry.UserId?.Value,
                entry.UserId is not null && usernamesById.TryGetValue(entry.UserId.Value, out var name) ? name : null,
                entry.ActionType,
                entry.Succeeded,
                entry.ErrorCode,
                entry.PayloadJson,
                entry.Timestamp))
            .ToList();

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<ListAuditLogResponse>.Success(
            new ListAuditLogResponse(items, page, pageSize, totalCount, totalPages));
    }
}
