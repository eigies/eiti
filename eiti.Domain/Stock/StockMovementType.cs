namespace eiti.Domain.Stock;

public enum StockMovementType
{
    ManualEntry = 1,
    ManualAdjustment = 2,
    Reserve = 3,
    ReleaseReservation = 4,
    SaleOut = 5,
    TradeInIn = 6,
    SaleReturn = 7,
    PurchaseIn = 8,
    PurchaseReturn = 9,
    TransferOut = 10,
    TransferIn = 11
}
