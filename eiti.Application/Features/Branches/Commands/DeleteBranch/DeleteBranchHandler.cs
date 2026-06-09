using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using MediatR;

namespace eiti.Application.Features.Branches.Commands.DeleteBranch;

public sealed class DeleteBranchHandler : IRequestHandler<DeleteBranchCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBranchHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.Id), companyId, cancellationToken);
        if (branch is null)
            return Result.Failure(DeleteBranchErrors.NotFound);

        // Borrado físico solo si la sucursal no tiene actividad (ventas, cajas, movimientos de stock,
        // usuarios asignados, o stock con cantidad > 0). Si tiene → se bloquea.
        if (await _branchRepository.IsReferencedAsync(branch.Id, companyId, cancellationToken))
            return Result.Failure(DeleteBranchErrors.InUse);

        // DeleteAsync limpia las filas de stock vacías (contadores en 0) de la sucursal y remueve la entidad.
        await _branchRepository.DeleteAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
