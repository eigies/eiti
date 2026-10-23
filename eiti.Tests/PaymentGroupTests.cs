using eiti.Domain.Customers;
using eiti.Domain.Purchases;
using eiti.Domain.Sales;
using eiti.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eiti.Tests;

// Orden de pago (proveedores) y recibo (clientes): la cabecera que agrupa varios medios en un comprobante.
public class PaymentGroupTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Date = new(2026, 10, 2);

    private static SupplierPaymentOrder NewOrder() =>
        SupplierPaymentOrder.Create(CompanyId, SupplierId, BranchId, "OP-0001", Date, "  Pago septiembre  ", UserId);

    private static SupplierPayment NewSupplierPayment(decimal amount, PurchasePaymentMethod method = PurchasePaymentMethod.Cash, Guid? supplierId = null) =>
        SupplierPayment.Create(CompanyId, supplierId ?? SupplierId, BranchId, method, amount, Date, null, null, UserId);

    private static CustomerReceipt NewReceipt() =>
        CustomerReceipt.Create(CompanyId, CustomerId, BranchId, "RC-0001", Date, null, UserId);

    private static CustomerPayment NewCustomerPayment(decimal amount, Guid? customerId = null) =>
        CustomerPayment.Create(CompanyId, customerId ?? CustomerId, BranchId, SalePaymentMethod.Cash, amount, Date, null, null, UserId);

    [Fact]
    public void Orden_NaceActivaEnCeroYConNotasNormalizadas()
    {
        var order = NewOrder();

        order.Status.Should().Be(PurchasePaymentStatus.Active);
        order.TotalAmount.Should().Be(0m);
        order.Notes.Should().Be("Pago septiembre");
        order.Code.Should().Be("OP-0001");
    }

    [Fact]
    public void Orden_RechazaCodigoVacio()
    {
        var act = () => SupplierPaymentOrder.Create(CompanyId, SupplierId, BranchId, " ", Date, null, UserId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Orden_AddPayment_VinculaElPagoYSumaElTotal()
    {
        var order = NewOrder();
        var cheque = NewSupplierPayment(350_000m, PurchasePaymentMethod.Check);
        var transfer = NewSupplierPayment(150_000m, PurchasePaymentMethod.BankTransfer);
        var cash = NewSupplierPayment(50_000.005m);

        order.AddPayment(cheque);
        order.AddPayment(transfer);
        order.AddPayment(cash);

        cheque.PaymentOrderId.Should().Be(order.Id);
        transfer.PaymentOrderId.Should().Be(order.Id);
        cash.PaymentOrderId.Should().Be(order.Id);
        order.TotalAmount.Should().Be(550_000.01m);
    }

    [Fact]
    public void Orden_AddPayment_RechazaPagoDeOtroProveedor()
    {
        var order = NewOrder();
        var act = () => order.AddPayment(NewSupplierPayment(100m, supplierId: Guid.NewGuid()));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Orden_AddPayment_RechazaPagoAnuladoOYaAgrupado()
    {
        var order = NewOrder();
        var cancelled = NewSupplierPayment(100m);
        cancelled.Cancel();
        var grouped = NewSupplierPayment(100m);
        NewOrder().AddPayment(grouped);

        ((Action)(() => order.AddPayment(cancelled))).Should().Throw<InvalidOperationException>();
        ((Action)(() => order.AddPayment(grouped))).Should().Throw<InvalidOperationException>();
        order.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public void Orden_Cancel_MarcaQuienYCuandoYNoSePuedeRepetirNiAgregar()
    {
        var order = NewOrder();
        var by = Guid.NewGuid();

        order.Cancel(by);

        order.Status.Should().Be(PurchasePaymentStatus.Cancelled);
        order.CancelledByUserId.Should().Be(by);
        order.CancelledAt.Should().NotBeNull();
        ((Action)(() => order.Cancel(by))).Should().Throw<InvalidOperationException>();
        ((Action)(() => order.AddPayment(NewSupplierPayment(100m)))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Recibo_AddPayment_VinculaElCobroYSumaElTotal()
    {
        var receipt = NewReceipt();
        var a = NewCustomerPayment(200_000m);
        var b = NewCustomerPayment(125_000m);

        receipt.AddPayment(a);
        receipt.AddPayment(b);

        a.ReceiptId.Should().Be(receipt.Id);
        b.ReceiptId.Should().Be(receipt.Id);
        receipt.TotalAmount.Should().Be(325_000m);
        receipt.Status.Should().Be(SaleCcPaymentStatus.Active);
    }

    [Fact]
    public void Recibo_AddPayment_RechazaCobroDeOtroClienteOAnulado()
    {
        var receipt = NewReceipt();
        var cancelled = NewCustomerPayment(100m);
        cancelled.Cancel();

        ((Action)(() => receipt.AddPayment(NewCustomerPayment(100m, Guid.NewGuid())))).Should().Throw<InvalidOperationException>();
        ((Action)(() => receipt.AddPayment(cancelled))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Recibo_Cancel_NoSePuedeRepetir()
    {
        var receipt = NewReceipt();
        receipt.Cancel(UserId);

        receipt.Status.Should().Be(SaleCcPaymentStatus.Cancelled);
        ((Action)(() => receipt.Cancel(UserId))).Should().Throw<InvalidOperationException>();
    }
}
