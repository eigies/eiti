-- Cobros "Otro" que duplican el canje de una venta CC. SOLO LEE.
-- Hasta el arreglo de los canjes (tarjeta, extracto e imputación no cargaban SaleTradeIns), una venta CC con canje
-- figuraba con el total entero pendiente, y para saldarla se cargó un cobro "Otro" por el valor del canje. Con el
-- arreglo desplegado esas ventas quedan cobradas de más y tools/LedgerCheck las marca con diferencia negativa.
-- Corrección: anular cada cobro listado desde la app ("Anular cobro"), DESPUÉS del deploy: con el código viejo la
-- venta volvería a pendiente y el stock a reservado. La venta sigue Pagada (la salda el canje) y el saldo a favor
-- no cambia.
--
-- Uso: psql "<conexión>" -f canjes-duplicados.sql

WITH canje AS (
    SELECT t."SaleId", SUM(t."Amount") AS canje
      FROM "SaleTradeIns" t
     GROUP BY 1
    HAVING SUM(t."Amount") > 0
)
SELECT co."Name"          AS empresa,
       cu."Name"          AS cliente,
       s."Code"           AS venta,
       k.canje,
       cp."Date"::date    AS fecha_cobro,
       cp."Amount"        AS cobro,
       cp."Reference"     AS referencia,
       cp."Id"            AS cobro_id
  FROM canje k
  JOIN "Sales" s            ON s."Id" = k."SaleId" AND s."IsCuentaCorriente" AND s."IdSaleStatus" <> 3
  JOIN "SaleCcPayments" x   ON x."SaleId" = s."Id" AND x."Status" = 1
  JOIN "CustomerPayments" cp ON cp."Id" = x."CustomerPaymentId" AND cp."Method" = 5 AND cp."Status" = 1
                            AND cp."Amount" = k.canje
  JOIN "Customers" cu       ON cu."Id" = s."CustomerId"
  JOIN "Companies" co       ON co."Id" = s."CompanyId"
 ORDER BY cp."Date";
