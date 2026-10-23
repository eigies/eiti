-- Saldo a favor que se perdió por el bug de "anular un pago/cobro" (arreglado en 6ba923b).
-- SOLO LEE. Usa tablas que ya existen en producción: no necesita la migración nueva.
--
-- Al registrar un pago, el sistema imputaba TODO el saldo a favor disponible (también el previo) con
-- back-link al pago. Al anularlo, la reversa hacía max(0, importe − imputado): cuando lo imputado
-- superaba al pago, la diferencia (saldo previo) desaparecía. Esta consulta busca pagos/cobros anulados
-- cuyas imputaciones superan su importe y suma esa diferencia por cuenta.
--
-- Correrla ANTES del deploy de la rama (o sobre la copia previa al deploy): los pagos no guardan cuándo se
-- anularon, y después del deploy las anulaciones nuevas ya devuelven el saldo pero aparecerían igual acá.
-- Es evidencia de apoyo: el número de referencia es la diferencia negativa que da tools/LedgerCheck.
-- Si los dos coinciden por cuenta, la causa es este bug.

-- Proveedores
WITH reversed AS (
    SELECT p."Id", p."CompanyId", p."SupplierId", p."Amount",
           COALESCE(SUM(pp."Amount") FILTER (WHERE pp."Status" = 2 AND pu."Status" <> 3), 0) AS imputed
    FROM "SupplierPayments" p
    LEFT JOIN "PurchasePayments" pp ON pp."SupplierPaymentId" = p."Id"
    LEFT JOIN "Purchases" pu ON pu."Id" = pp."PurchaseId"
    WHERE p."Status" = 2                         -- pago anulado
    GROUP BY p."Id", p."CompanyId", p."SupplierId", p."Amount"
)
SELECT 'proveedor' AS cuenta, c."Name" AS empresa, s."Name" AS nombre, s."Id" AS id,
       COUNT(*) AS anulaciones, SUM(r.imputed - r."Amount") AS saldo_perdido, s."CreditBalance" AS saldo_a_favor_actual
FROM reversed r
JOIN "Suppliers" s ON s."Id" = r."SupplierId"
JOIN "Companies" c ON c."Id" = r."CompanyId"
WHERE r.imputed > r."Amount"
GROUP BY c."Name", s."Name", s."Id", s."CreditBalance"

UNION ALL

-- Clientes
SELECT 'cliente', c."Name", cu."Name", cu."Id", COUNT(*), SUM(r.imputed - r."Amount"), cu."CreditBalance"
FROM (
    SELECT p."Id", p."CompanyId", p."CustomerId", p."Amount",
           COALESCE(SUM(sc."Amount") FILTER (WHERE sc."Status" = 2 AND sa."IdSaleStatus" <> 3), 0) AS imputed
    FROM "CustomerPayments" p
    LEFT JOIN "SaleCcPayments" sc ON sc."CustomerPaymentId" = p."Id"
    LEFT JOIN "Sales" sa ON sa."Id" = sc."SaleId"
    WHERE p."Status" = 2                         -- cobro anulado
    GROUP BY p."Id", p."CompanyId", p."CustomerId", p."Amount"
) r
JOIN "Customers" cu ON cu."Id" = r."CustomerId"
JOIN "Companies" c ON c."Id" = r."CompanyId"
WHERE r.imputed > r."Amount"
GROUP BY c."Name", cu."Name", cu."Id", cu."CreditBalance"
ORDER BY saldo_perdido DESC;
