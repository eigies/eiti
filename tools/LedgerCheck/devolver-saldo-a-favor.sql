-- Devuelve el saldo a favor perdido por el bug de anular pagos/cobros. ESCRIBE: correr solo con el OK del dueño.
--
-- Una línea por cuenta CONFIRMADA (la diferencia negativa de tools/LedgerCheck coincide con saldo_perdido de
-- saldo-a-favor-perdido.sql). Cada línea exige que el saldo actual sea exactamente el diagnosticado: si alguien
-- operó la cuenta en el medio, aborta y no toca nada (todo va en una sola transacción).
--
-- En producción, aplicarlo DESPUÉS del deploy de la rama: con el código viejo corriendo, una anulación
-- nueva podría volver a perder saldo entre el diagnóstico y la corrección.
--
-- Uso: completar las llamadas de abajo y correr con  psql "<conexión>" -v ON_ERROR_STOP=1 -f devolver-saldo-a-favor.sql

BEGIN;

CREATE OR REPLACE FUNCTION pg_temp.devolver_saldo(tipo text, cuenta uuid, saldo_actual numeric, devolver numeric)
RETURNS void AS $$
DECLARE n int;
BEGIN
    IF tipo = 'proveedor' THEN
        UPDATE "Suppliers" SET "CreditBalance" = "CreditBalance" + devolver
         WHERE "Id" = cuenta AND "CreditBalance" = saldo_actual;
    ELSIF tipo = 'cliente' THEN
        UPDATE "Customers" SET "CreditBalance" = "CreditBalance" + devolver, "UpdatedAt" = timezone('utc', now())
         WHERE "Id" = cuenta AND "CreditBalance" = saldo_actual;
    ELSE
        RAISE EXCEPTION 'Tipo inválido: %', tipo;
    END IF;
    GET DIAGNOSTICS n = ROW_COUNT;
    IF n <> 1 THEN
        RAISE EXCEPTION '% %: el saldo a favor actual no es %, no se toca nada', tipo, cuenta, saldo_actual;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- tipo,        id de la cuenta,                          saldo a favor actual, saldo a devolver
-- SELECT pg_temp.devolver_saldo('proveedor', '00000000-0000-0000-0000-000000000000', 0.00, 0.00);
-- SELECT pg_temp.devolver_saldo('cliente',   '00000000-0000-0000-0000-000000000000', 0.00, 0.00);

COMMIT;
