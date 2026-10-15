# DemoSeed

Llena la base **local** con una casa de baterías ficticia ("Baterías San Martín") con ~3,5 meses de
operación, para hacer demos con dashboards y pantallas cargadas.

Qué genera (todo coherente entre sí, simulado en orden cronológico como lo harían los handlers):

- 2 sucursales (Casa Central / Sucursal Norte), una caja cada una, 27 productos (Moura, Willard, Bosch,
  Heliar, Prestolite, Yuasa, accesorios y casco para canje), 70 clientes minoristas y 10 mayoristas CC.
- ~950 ventas (importes y volumen inventados y escalados: no se corresponden con ningún cliente real): minoristas pagadas en efectivo, transferencia o tarjeta (con canje de casco), ventas
  CC a talleres/flotas, pendientes y canceladas. El mes en curso viene un poco arriba del anterior.
- Una sesión de caja por día hábil y sucursal: abre con lo que quedó en el cierre anterior, retiro a
  tesorería al cierre y alguna diferencia chica de arqueo. **Hoy:** Casa Central queda **abierta** y
  Sucursal Norte **cerrada** (turno mañana).
- Cobros a mayoristas (FIFO, con cheques en cartera/depositados/acreditados), compras semanales a
  proveedores (las últimas impagas), stock con algunos productos bajos y presupuestos.

## Uso

1. Levantar la API una vez y registrar la empresa (crea perfiles de acceso y el owner con el flujo real):

   ```bash
   curl -X POST http://localhost:5133/api/auth/register -H "Content-Type: application/json" \
     -d '{"username":"demo","firstName":"Gustavo","lastName":"Demo","email":"demo@bateriassanmartin.com.ar","password":"Demo1234!","companyName":"Baterias San Martin"}'
   ```

2. Correr el seed (requiere `pip install psycopg2-binary`):

   ```bash
   python -I tools/DemoSeed/seed_demo.py
   ```

   Opcionales: `--owner demo`, `--days 105`, `--seed <n>`, `--price-factor 0.8`, `--volume-factor 0.75`, `--conn "<libpq conn string>"` (o env `EITI_DEMO_CONN`).

**Correrlo el día de la demo.** Es idempotente (borra lo operativo de esa empresa y lo regenera relativo a
"ahora"), y la caja abierta tiene que ser de hoy: con una caja de un día anterior el sistema no deja vender.

Usuarios: `demo` (owner), `cajero.centro` (Cajero, Casa Central) y `vendedor.norte` (Vendedor, Sucursal
Norte), todos con password `Demo1234!`.

Solo toca filas de la empresa del owner indicado; no usar contra producción.
