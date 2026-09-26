using eiti.Domain.Customers;

namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Datos del cliente que el fisco exige para emitir, validados ANTES de pedir el comprobante.
/// El servicio de facturación valida lo mismo, pero contesta después de guardada la venta y en
/// términos técnicos; acá se corta antes y con un mensaje que el vendedor puede resolver.
///
/// El tope de identificación del consumidor final NO se valida acá a propósito: el monto lo fija
/// ARCA y cambia seguido, y vive configurado en un solo lugar (el servicio). Duplicarlo en EITI
/// obligaría a actualizar dos números cada vez que ARCA lo mueve.
/// </summary>
public static class SaleInvoicingReceiverRules
{
    private static readonly int[] CuitWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>Null si se puede facturar al cliente; si no, el motivo para mostrarle al usuario.</summary>
    public static string? Validate(Customer? customer)
    {
        // Sin cliente es Consumidor Final sin identificar: siempre se puede (hasta el tope de ARCA).
        if (customer?.IvaCondition is not (IvaCondition.ResponsableInscripto or IvaCondition.Monotributo))
        {
            return null;
        }

        var condition = customer.IvaCondition == IvaCondition.ResponsableInscripto
            ? "Responsable Inscripto"
            : "Monotributista";

        var cuit = OnlyDigits(customer.TaxId);
        if (cuit is null)
        {
            return $"Para facturar a {customer.FullName} ({condition}) hay que cargar su CUIT en la ficha del cliente.";
        }

        return IsValidCuit(cuit)
            ? null
            : $"El CUIT de {customer.FullName} no es válido. Revisalo en la ficha del cliente.";
    }

    /// <summary>11 dígitos con dígito verificador módulo 11, el mismo cálculo que hace ARCA.</summary>
    public static bool IsValidCuit(string digits)
    {
        if (digits.Length != 11 || !digits.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < CuitWeights.Length; i++)
        {
            sum += (digits[i] - '0') * CuitWeights[i];
        }

        var expected = 11 - sum % 11;
        expected = expected switch
        {
            11 => 0,
            10 => 9,
            _ => expected
        };

        return digits[10] - '0' == expected;
    }

    internal static string? OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
