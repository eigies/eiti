using eiti.Application.Common;

namespace eiti.Application.Features.Stock.Commands.TransferStock;

public static class TransferStockErrors
{
    public static readonly Error BranchRestricted = Error.Forbidden(
        "Stock.Transfer.BranchRestricted",
        "No tenés permiso para transferir stock entre sucursales.");

    public static readonly Error SameBranch = Error.Validation(
        "Stock.Transfer.SameBranch",
        "La sucursal de origen y la de destino deben ser distintas.");

    public static readonly Error NoItems = Error.Validation(
        "Stock.Transfer.NoItems",
        "Debés agregar al menos un producto a transferir.");

    public static readonly Error DuplicateProduct = Error.Validation(
        "Stock.Transfer.DuplicateProduct",
        "No podés repetir el mismo producto en el traspaso.");

    public static readonly Error SourceBranchNotFound = Error.NotFound(
        "Stock.Transfer.SourceBranchNotFound",
        "La sucursal de origen no fue encontrada.");

    public static readonly Error DestinationBranchNotFound = Error.NotFound(
        "Stock.Transfer.DestinationBranchNotFound",
        "La sucursal de destino no fue encontrada.");

    public static Error ProductNotFound(Guid productId) => Error.NotFound(
        "Stock.Transfer.ProductNotFound",
        $"Un producto seleccionado no fue encontrado ({productId}).");

    public static Error InsufficientStock(string productName, int available, int requested) => Error.Conflict(
        "Stock.Transfer.InsufficientStock",
        $"Stock insuficiente de '{productName}' en la sucursal de origen: disponible {available}, solicitado {requested}.");
}
