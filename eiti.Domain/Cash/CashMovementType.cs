namespace eiti.Domain.Cash;

public enum CashMovementType
{
    OpeningFloat = 1,
    SaleIncome = 2,
    CashWithdrawal = 3,
    ManualAdjustment = 4,
    CashTransferOut = 5,
    CashTransferIn = 6,
    SaleCancellation = 7,
    CuentaCorrienteIncome = 8,
    CuentaCorrienteCancellation = 9,
    TransferIncome = 10,
    CardIncome = 11,
    PurchaseExpense = 12,
    CashDeposit = 13,
    PurchasePaymentCancellation = 14,
    PayrollExpense = 15,
    PayrollExpenseCancellation = 16,
    PayrollAdvanceExpense = 17,
    PayrollAdvanceExpenseCancellation = 18,

    // Notas de crédito: se ven en la sesión pero no mueven efectivo (Direction.None).
    CustomerCreditNote = 19,
    CustomerCreditNoteCancellation = 20,
    SupplierCreditNote = 21,
    SupplierCreditNoteCancellation = 22
}
