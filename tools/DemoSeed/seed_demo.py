#!/usr/bin/env python3
"""Seed de demo: una casa de baterías con ~3 meses de operación, para mostrar el sistema.

Simula la operación día por día, en orden cronológico, replicando lo que hacen los handlers
reales (CreateSale, CreateCcSale, AddCustomerPayment, CreatePurchase, AddSupplierPayment):

  - Ventas minoristas pagadas (efectivo / transferencia / tarjeta) con canje de casco,
    imputadas a la caja abierta de la sucursal. Reserve + SaleOut de stock.
  - Ventas cuenta corriente a mayoristas (talleres, flotas): reservan stock y quedan OnHold
    hasta que un cobro a nivel cliente (FIFO) las salda -> Paid + SaleOut.
  - Ventas pendientes y canceladas (Reserve / ReleaseReservation).
  - Una sesión de caja por día y sucursal, con cierre y diferencias de arqueo chicas.
    HOY: Casa Central queda ABIERTA; Sucursal Norte cierra al mediodía (turno mañana).
  - Compras semanales a proveedores (PurchaseIn) pagadas por transferencia días después;
    las más recientes quedan impagas (deuda con proveedores).
  - Cheques de clientes en cartera / depositados / acreditados, presupuestos, usuarios
    cajero y vendedor.

Requisitos: la empresa ya registrada con el flujo real (crea perfiles de acceso y owner):

  curl -X POST http://localhost:5133/api/auth/register -H "Content-Type: application/json" \
    -d '{"username":"demo","firstName":"Gustavo","lastName":"Demo","email":"demo@bateriassanmartin.com.ar","password":"Demo1234!","companyName":"Baterias San Martin"}'

Es idempotente: borra TODO lo operativo de esa empresa (menos la empresa, sus perfiles y el
owner) y lo vuelve a generar relativo a "ahora". Correrlo el mismo día de la demo: la caja
abierta tiene que ser de hoy o el sistema no deja vender (CashSessionFromPreviousDay).

  python -I tools/DemoSeed/seed_demo.py [--owner demo] [--conn "..."] [--days 105]
"""

import argparse
import base64
import hashlib
import os
import random
import sys
import uuid
from collections import defaultdict
from datetime import date, datetime, time, timedelta, timezone
from decimal import ROUND_HALF_UP, Decimal

import psycopg2
import psycopg2.extras

psycopg2.extras.register_uuid()

AR_OFFSET = timedelta(hours=3)  # Argentina UTC-3, sin horario de verano
DEFAULT_CONN = "host=localhost port=5432 dbname=eitidb user=postgres password=postgres"
DEMO_PASSWORD = "Demo1234!"
HOLIDAYS = {date(2026, 7, 9), date(2026, 8, 17)}
FLOAT_BY_BRANCH = [50000, 30000]  # fondo de cambio que queda en el cajón al cerrar

# Estacionalidad: el invierno es temporada alta de baterías.
MONTH_FACTOR = {6: 1.10, 7: 1.12, 8: 1.05, 9: 0.94, 10: 1.02, 11: 0.98, 12: 1.0}
WEEKDAY_FACTOR = {0: 1.15, 1: 1.0, 2: 0.95, 3: 1.0, 4: 1.08, 5: 0.6}

D0 = Decimal("0.00")


def money(x) -> Decimal:
    return Decimal(str(x)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)


def utc(local_dt: datetime) -> datetime:
    return local_dt + AR_OFFSET


def new_id() -> uuid.UUID:
    return uuid.uuid4()


def hash_password(password: str) -> str:
    salt = os.urandom(16)
    key = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 100_000, 32)
    return f"100000.{base64.b64encode(salt).decode()}.{base64.b64encode(key).decode()}"


# ---------------------------------------------------------------------------------------------
# Catálogo
# ---------------------------------------------------------------------------------------------

CATEGORIES = ["Baterías auto", "Baterías camión y utilitario", "Baterías moto", "Accesorios", "Cascos (usadas)"]

# code, brand, name, category, price, cost, peso de venta minorista, peso mayorista, stock objetivo central
PRODUCTS = [
    ("MOU-M18FD", "Moura", "Batería Moura M18FD 12x65", 0, 179900, 124500, 14, 10, 30),
    ("MOU-M20GD", "Moura", "Batería Moura M20GD 12x75", 0, 209900, 146200, 13, 10, 30),
    ("MOU-M22GD", "Moura", "Batería Moura M22GD 12x80", 0, 229900, 160100, 6, 5, 16),
    ("MOU-M24KD", "Moura", "Batería Moura M24KD 12x85", 0, 249900, 174300, 5, 5, 14),
    ("MOU-M26AD", "Moura", "Batería Moura M26AD 12x90", 0, 269900, 188000, 3, 4, 10),
    ("WIL-UB620", "Willard", "Batería Willard UB 620 12x65", 0, 169900, 115800, 9, 7, 22),
    ("WIL-UB740", "Willard", "Batería Willard UB 740 12x75", 0, 199900, 136400, 8, 7, 20),
    ("WIL-UB840", "Willard", "Batería Willard UB 840 12x85", 0, 236900, 161900, 3, 3, 10),
    ("BOS-S4-45", "Bosch", "Batería Bosch S4 12x45", 0, 139900, 96300, 4, 1, 10),
    ("BOS-S4-65", "Bosch", "Batería Bosch S4 12x65", 0, 189900, 131100, 5, 2, 12),
    ("BOS-S5-75", "Bosch", "Batería Bosch S5 12x75 Silver", 0, 224900, 156200, 3, 1, 8),
    ("HEL-FREE65", "Heliar", "Batería Heliar Free 12x65", 0, 159900, 108700, 4, 4, 12),
    ("HEL-FREE75", "Heliar", "Batería Heliar Free 12x75", 0, 184900, 126100, 3, 3, 10),
    ("PRE-65", "Prestolite", "Batería Prestolite 12x65", 0, 149900, 103200, 3, 4, 10),
    ("MOU-M30LD", "Moura", "Batería Moura M30LD 12x110", 1, 329900, 231400, 2, 5, 10),
    ("WIL-UB1040", "Willard", "Batería Willard UB 1040 12x110", 1, 315900, 219300, 1, 4, 8),
    ("MOU-M150BD", "Moura", "Batería Moura M150BD 12x150", 1, 449900, 318700, 1, 3, 6),
    ("WIL-UB1240", "Willard", "Batería Willard UB 1240 12x180", 1, 529900, 372100, 0.5, 2, 5),
    ("YUA-YTX7L", "Yuasa", "Batería Yuasa YTX7L-BS moto", 2, 69900, 46300, 3, 0.5, 10),
    ("YUA-YTX9", "Yuasa", "Batería Yuasa YTX9-BS moto", 2, 79900, 53400, 2, 0.5, 8),
    ("MOU-MA5D", "Moura", "Batería Moura MA5-D moto", 2, 54900, 36100, 2, 0.5, 8),
    ("ACC-BORNES", "Genérico", "Par de bornes de bronce", 3, 8900, 4200, 0, 0, 40),
    ("ACC-AGUA", "Genérico", "Agua desmineralizada 1 L", 3, 3500, 1400, 0, 0, 40),
    ("ACC-CABLES", "Genérico", "Cables puente 600A 3 m", 3, 24900, 13600, 0, 0, 12),
    ("ACC-CARG10", "Black+Decker", "Cargador de baterías 12V 10A", 3, 89900, 58200, 0, 0, 6),
    ("ACC-ARRANQ", "Black+Decker", "Arrancador portátil 12V 700A", 3, 139900, 92300, 0, 0, 4),
    ("CASCO", "Usada", "Batería usada (casco)", 4, 15000, 8000, 0, 0, 0),
]

SUPPLIER_BY_BRAND = {
    "Moura": "Distribuidora Litoral Baterías S.A.",
    "Willard": "Acumuladores del Oeste S.R.L.",
    "Heliar": "Acumuladores del Oeste S.R.L.",
    "Prestolite": "Acumuladores del Oeste S.R.L.",
    "Bosch": "Autopartes Bonaerense S.A.",
    "Yuasa": "Moto Parts Importadora S.A.",
    "Genérico": "Insumos Automotor Ramos S.R.L.",
    "Black+Decker": "Insumos Automotor Ramos S.R.L.",
}

SUPPLIERS = [
    ("Distribuidora Litoral Baterías S.A.", "30-71234567-1", "011 4651-2200", "pedidos@litoralbaterias.com.ar"),
    ("Acumuladores del Oeste S.R.L.", "30-70987654-3", "011 4488-9100", "ventas@acumuladoresoeste.com.ar"),
    ("Autopartes Bonaerense S.A.", "30-69876543-2", "011 4205-7700", "comercial@autopartesba.com.ar"),
    ("Moto Parts Importadora S.A.", "30-71555444-9", "011 4762-3300", "mayoristas@motoparts.com.ar"),
    ("Insumos Automotor Ramos S.R.L.", "30-71888777-5", "011 4656-1122", "insumos@iar.com.ar"),
]

# Mayoristas (cuenta corriente): name, cuit, iva, peso, prefiere camión, días entre cobros, sucursal (0=central)
WHOLESALE = [
    ("Taller Mecánico Hermanos Ruiz", "30-71100223-4", 1, 10, False, 7, 0),
    ("Gomería y Lubricentro El Rápido", "20-28765432-1", 2, 8, False, 6, 0),
    ("Transportes Del Valle S.R.L.", "30-70456789-0", 1, 7, True, 14, 0),
    ("Lubricentro Ruta 3", "20-31234567-8", 2, 6, False, 8, 1),
    ("Electricidad del Automotor Gómez", "20-25987654-3", 2, 8, False, 7, 0),
    ("Remises San Justo S.A.", "30-71234000-6", 1, 5, False, 10, 0),
    ("Agroservicios La Pampa S.A.", "30-68765123-7", 1, 3, True, 15, 0),
    ("Taller Integral Martínez", "20-29345678-5", 2, 6, False, 7, 1),
    ("Distribuidora de Bebidas Morón S.A.", "30-70111222-8", 1, 4, True, 12, 0),
    ("Mecánica Ligera Fernández", "20-33456789-2", 2, 5, False, 9, 1),
]

FIRST_NAMES = ["Juan", "María", "Carlos", "Laura", "Diego", "Sofía", "Martín", "Valeria", "Pablo", "Florencia",
               "Sergio", "Carolina", "Hernán", "Gabriela", "Nicolás", "Romina", "Federico", "Paula", "Alejandro",
               "Natalia", "Gustavo", "Silvina", "Ezequiel", "Mariana", "Leandro", "Daniela", "Ricardo", "Analía",
               "Facundo", "Lorena", "Javier", "Verónica", "Cristian", "Andrea", "Marcelo", "Julieta"]
LAST_NAMES = ["González", "Rodríguez", "Gómez", "Fernández", "López", "Díaz", "Martínez", "Pérez", "García",
              "Sánchez", "Romero", "Sosa", "Álvarez", "Torres", "Ruiz", "Ramírez", "Flores", "Acosta", "Benítez",
              "Medina", "Herrera", "Suárez", "Aguirre", "Giménez", "Molina", "Castro", "Ortiz", "Silva", "Rojas",
              "Luna", "Juárez", "Cabrera", "Ríos", "Ferreyra", "Godoy", "Morales"]

RETAIL_CHANNELS = [(7, 25), (1, 20), (2, 15), (9, 15), (5, 10), (10, 10), (8, 5)]  # SaleSourceChannel, peso


class Seeder:
    def __init__(self, conn, owner_username: str, days: int, rng_seed: int,
                 price_factor: float = 0.8, volume_factor: float = 0.75):
        self.conn = conn
        # Importes y volumen "dibujados": la demo no tiene que poder compararse con ningún cliente real.
        self.price_factor = Decimal(str(price_factor))
        self.volume_factor = volume_factor
        self.cur = conn.cursor()
        self.rng = random.Random(rng_seed)
        self.rows = defaultdict(list)
        self.now_utc = datetime.now(timezone.utc).replace(tzinfo=None)
        self.now_local = self.now_utc - AR_OFFSET
        self.today = self.now_local.date()
        self.start_day = self.today - timedelta(days=days)

        self.cur.execute('select "CompanyId", "Id" from "Users" where "Username" = %s', (owner_username,))
        found = self.cur.fetchone()
        if not found:
            sys.exit(f"No existe el usuario '{owner_username}'. Registrá la empresa primero (ver docstring).")
        self.company_id, self.owner_id = found
        self.owner_username = owner_username
        self.cur.execute('select "SystemKey", "Id" from "AccessProfiles" where "CompanyId" = %s', (self.company_id,))
        self.profiles = dict(self.cur.fetchall())

    # -----------------------------------------------------------------------------------------
    def scaled(self, amount) -> Decimal:
        return money((Decimal(amount) * self.price_factor / 500).to_integral_value() * 500)

    def add(self, table, **cols):
        self.rows[table].append(cols)
        return cols

    def wipe(self):
        c = self.company_id
        sales = 'select "Id" from "Sales" where "CompanyId" = %(c)s'
        purchases = 'select "Id" from "Purchases" where "CompanyId" = %(c)s'
        liqs = 'select "Id" from "PayrollLiquidations" where "CompanyId" = %(c)s'
        users = 'select "Id" from "Users" where "CompanyId" = %(c)s'
        by_company = '"CompanyId" = %(c)s'
        statements = [
            ("CashMovements", f'"CashSessionId" in (select "Id" from "CashSessions" where {by_company})'),
            ("SaleCcPayments", f'"SaleId" in ({sales})'),
            ("SalePayments", f'"SaleId" in ({sales})'),
            ("SaleTradeIns", f'"SaleId" in ({sales})'),
            ("SaleDetails", f'"SaleId" in ({sales})'),
            ("SaleFiscalDocuments", by_company),
            ("SaleTransportAssignments", by_company),
            ("QuoteDetails", f'"QuoteId" in (select "Id" from "Quotes" where {by_company})'),
            ("Quotes", by_company),
            ("Cheques", by_company),
            ("CustomerCreditNotes", by_company),
            ("CustomerPayments", by_company),
            ("Sales", by_company),
            ("StockMovements", by_company),
            ("BranchProductStocks", by_company),
            ("PurchasePayments", f'"PurchaseId" in ({purchases})'),
            ("PurchaseDetails", f'"PurchaseId" in ({purchases})'),
            ("SupplierCreditNotes", by_company),
            ("SupplierPayments", by_company),
            ("Purchases", by_company),
            ("CashDrawerUserAssignments", f'"CashDrawerId" in (select "Id" from "CashDrawers" where {by_company})'),
            ("CashSessions", by_company),
            ("CashDrawers", by_company),
            ("Products", by_company),
            ("ProductCategories", by_company),
            ("Customers", by_company),
            ("Suppliers", by_company),
            ("BankInstallmentPlans", f'"BankId" in (select "Id" from "Banks" where {by_company})'),
            ("Banks", by_company),
            ("PayrollLiquidationAdvanceLines", f'"PayrollLiquidationId" in ({liqs})'),
            ("PayrollLiquidationBonusLines", f'"PayrollLiquidationId" in ({liqs})'),
            ("PayrollLiquidationDeductionLines", f'"PayrollLiquidationId" in ({liqs})'),
            ("PayrollLiquidations", by_company),
            ("PayrollAdvances", by_company),
            ("PayrollBonuses", by_company),
            ("PayrollBonusConcepts", by_company),
            ("PayrollDeductionConcepts", by_company),
            ("FleetLogs", by_company),
            ("Vehicles", by_company),
            ("DriverProfiles", by_company),
            ("UserBranchAccesses", f'"UserId" in ({users})'),
            ("UserRoleAudits", by_company),
            ("AuditLogs", by_company),
            ("PasswordResetTokens", f'"UserId" in ({users} and "Id" <> %(o)s)'),
            ("Users", f'{by_company} and "Id" <> %(o)s'),
            ("Employees", by_company),
            ("Branches", by_company),
            ("FiscalPointsOfSale", by_company),
        ]
        # FKs apagadas solo durante el borrado: el orden de arriba es best-effort.
        self.cur.execute("SET session_replication_role = replica")
        for table, where in statements:
            self.cur.execute(f'delete from "{table}" where {where}', {"c": c, "o": self.owner_id})
        self.cur.execute("SET session_replication_role = DEFAULT")

    # -----------------------------------------------------------------------------------------
    def build_master_data(self):
        r = self.rng
        created = utc(datetime.combine(self.start_day - timedelta(days=20), time(10, 0)))
        self.master_created = created

        # Puntos de venta y sucursales
        self.branches = []
        for idx, (name, code, addr) in enumerate([
            ("Casa Central", "CEN", "Av. de Mayo 1450, Ramos Mejía"),
            ("Sucursal Norte", "NOR", "Av. Gaona 3820, Haedo"),
        ]):
            pos_id = new_id()
            self.add("FiscalPointsOfSale", Id=pos_id, CompanyId=self.company_id, Number=idx + 1, CreatedAt=created)
            bid = new_id()
            self.add("Branches", Id=bid, CompanyId=self.company_id, Name=name, Code=code, Address=addr,
                     CreatedAt=created, AutomaticInvoicing=None, FiscalPointOfSaleId=pos_id)
            self.branches.append({"id": bid, "code": code, "name": name, "seq": 0, "quote_seq": 0})

        # Bancos (Id identity: se insertan ya para tener los ids). Las secuencias pueden haber quedado
        # atrasadas si la base se armó copiando filas con Id explícito (tools/DbCopy).
        for table in ("Banks", "BankInstallmentPlans"):
            self.cur.execute(
                f"""select setval(pg_get_serial_sequence('"{table}"', 'Id'),
                                  greatest((select coalesce(max("Id"), 0) from "{table}"), 1))""")
        banks = [
            ("Banco Galicia", True, True, True, [(3, 0), (6, 12)]),
            ("Banco Nación", True, True, True, [(3, 0)]),
            ("Banco Santander", True, True, True, [(3, 0), (6, 14)]),
            ("Banco Macro", True, True, True, []),
            ("Mercado Pago", False, False, True, []),
            ("Naranja X", True, False, False, [(3, 8)]),
        ]
        self.card_banks, self.transfer_banks, self.cheque_banks = [], [], []
        for name, card, cheque, transfer, plans in banks:
            self.cur.execute(
                'insert into "Banks" ("CompanyId","Name","Active","CreatedAt","UpdatedAt","UseForCard","UseForCheque","UseForTransfer") '
                'values (%s,%s,true,%s,%s,%s,%s,%s) returning "Id"',
                (self.company_id, name, created, created, card, cheque, transfer))
            bank_id = self.cur.fetchone()[0]
            for cuotas, pct in plans:
                self.add("BankInstallmentPlans", BankId=bank_id, Cuotas=cuotas, SurchargePct=money(pct), Active=True,
                         CreatedAt=created)
            if card:
                self.card_banks.append(bank_id)
            if transfer:
                self.transfer_banks.append(bank_id)
            if cheque:
                self.cheque_banks.append(bank_id)

        # Usuarios operativos
        pw = hash_password(DEMO_PASSWORD)
        self.cashier_id = new_id()
        self.seller_id = new_id()
        for uid, username, first, last, profile, branch in [
            (self.cashier_id, "cajero.centro", "Lucía", "Fernández", "cashier", self.branches[0]),
            (self.seller_id, "vendedor.norte", "Matías", "Gómez", "seller", self.branches[1]),
        ]:
            self.add("Users", Id=uid, Username=username, Email=f"{username}@bateriassanmartin.com.ar", PasswordHash=pw,
                     CompanyId=self.company_id, EmployeeId=None, AccessProfileId=self.profiles[profile], IsActive=True,
                     CreatedAt=created, LastLoginAt=self.now_utc - timedelta(hours=2), FirstName=first, LastName=last)
            self.add("UserBranchAccesses", Id=new_id(), UserId=uid, BranchId=branch["id"])
        self.branches[0]["user"] = self.cashier_id
        self.branches[1]["user"] = self.seller_id

        # Cajas
        for branch, drawer_name in zip(self.branches, ["Caja Mostrador", "Caja Principal"]):
            did = new_id()
            branch["drawer"] = did
            self.add("CashDrawers", Id=did, CompanyId=self.company_id, BranchId=branch["id"], Name=drawer_name,
                     IsActive=True, CreatedAt=created)
            self.add("CashDrawerUserAssignments", Id=new_id(), CashDrawerId=did, UserId=branch["user"], AssignedAt=created)

        # Catálogo
        cat_ids = []
        for name in CATEGORIES:
            cid = new_id()
            cat_ids.append(cid)
            self.add("ProductCategories", Id=cid, CompanyId=self.company_id, Name=name, CreatedAt=created)
        self.products = []
        for i, (code, brand, name, cat, price, cost, w_retail, w_cc, target) in enumerate(PRODUCTS):
            pid = new_id()
            # Precio con desvío propio por producto, redondeado a "xx.900" como una lista de precios real.
            jitter = Decimal(str(r.uniform(0.88, 1.12)))
            fake_price = Decimal(price) * self.price_factor * jitter
            step = 1000 if fake_price >= 20000 else 100
            fake_price = max(Decimal(step), (fake_price / step).to_integral_value() * step - step // 10)
            margin = Decimal(cost) / Decimal(price) * Decimal(str(r.uniform(0.96, 1.04)))
            p = {"id": pid, "code": code, "brand": brand, "name": name, "cat": cat, "price": money(fake_price),
                 "cost": money((fake_price * margin / 100).to_integral_value() * 100), "w_retail": w_retail,
                 "w_cc": w_cc, "target": target}
            self.products.append(p)
            self.add("Products", Id=pid, CompanyId=self.company_id, Code=code, Sku=f"779{1000000 + i * 7919:07d}",
                     Brand=brand, Name=name, Description=None, Price=p["price"], CostPrice=p["cost"], UnitPrice=None,
                     AllowsManualValueInSale=(code == "CASCO"), NoDeliverySurcharge=None, CategoryId=cat_ids[cat],
                     CreatedAt=created)
        self.by_code = {p["code"]: p for p in self.products}
        self.casco = self.by_code["CASCO"]
        self.batteries = [p for p in self.products if p["cat"] in (0, 1, 2)]
        self.car_batteries = [p for p in self.products if p["cat"] == 0]
        self.truck_batteries = [p for p in self.products if p["cat"] == 1]
        self.moto_batteries = [p for p in self.products if p["cat"] == 2]

        # Proveedores
        self.suppliers = {}
        for name, cuit, phone, email in SUPPLIERS:
            sid = new_id()
            self.suppliers[name] = {"id": sid, "name": name, "credit": D0}
            self.add("Suppliers", Id=sid, CompanyId=self.company_id, Name=name, Phone=phone, Email=email,
                     TaxId=cuit.replace("-", ""), Notes=None, IsActive=True, CreatedAt=created, CreditBalance=D0)

        # Clientes minoristas
        self.retail_customers = []
        used_names, used_dnis = set(), set()
        while len(self.retail_customers) < 70:
            first, last = r.choice(FIRST_NAMES), r.choice(LAST_NAMES)
            if (first, last) in used_names:
                continue
            used_names.add((first, last))
            cid = new_id()
            dni = str(r.randint(14_000_000, 44_000_000))
            if dni in used_dnis:
                continue
            used_dnis.add(dni)
            phone = f"11 {r.randint(3000, 6999)}-{r.randint(1000, 9999)}"
            c = {"id": cid, "name": f"{first} {last}", "phone": phone}
            self.retail_customers.append(c)
            self.add("Customers", Id=cid, CompanyId=self.company_id, Name=c["name"], FirstName=first, LastName=last,
                     Email=None, Phone=phone, DocumentType=1, DocumentNumber=dni, TaxId=None, AddressId=None,
                     CreditBalance=D0, CreatedAt=created + timedelta(days=r.randint(0, 15)), IvaCondition=3)

        # Mayoristas (CC)
        self.wholesale = []
        for name, cuit, iva, weight, truck, every, branch_idx in WHOLESALE:
            cid = new_id()
            c = {"id": cid, "name": name, "cuit": cuit.replace("-", ""), "weight": weight, "truck": truck,
                 "every": every, "branch": self.branches[branch_idx], "credit": D0,
                 "next_payment": self.start_day + timedelta(days=r.randint(3, every + 2)),
                 "phone": f"11 {r.randint(4000, 4999)}-{r.randint(1000, 9999)}", "row": None}
            c["row"] = self.add("Customers", Id=cid, CompanyId=self.company_id, Name=name, FirstName=name, LastName="",
                                Email=None, Phone=c["phone"], DocumentType=None, DocumentNumber=None, TaxId=c["cuit"],
                                AddressId=None, CreditBalance=D0, CreatedAt=created, IvaCondition=iva)
            self.wholesale.append(c)

        # Stock inicial por sucursal
        self.stock = {}
        init_at = utc(datetime.combine(self.start_day - timedelta(days=1), time(18, 0)))
        for b_idx, branch in enumerate(self.branches):
            for p in self.products:
                sid = new_id()
                target = p["target"] if b_idx == 0 or p["target"] == 0 else max(2, round(p["target"] * 0.5))
                s = {"id": sid, "branch": branch, "product": p, "on_hand": 0, "reserved": 0, "target": target}
                self.stock[(branch["id"], p["id"])] = s
                if target > 0:
                    s["on_hand"] = target
                    self.stock_move(s, 1, target, None, None, "Stock inicial", init_at, self.owner_id)

    # -----------------------------------------------------------------------------------------
    def stock_move(self, s, mtype, qty, ref_type, ref_id, description, at, user):
        self.add("StockMovements", Id=new_id(), CompanyId=self.company_id, BranchId=s["branch"]["id"],
                 ProductId=s["product"]["id"], BranchProductStockId=s["id"], Type=mtype, Quantity=qty,
                 ReferenceType=ref_type, ReferenceId=ref_id, Description=description, CreatedAt=at,
                 CreatedByUserId=user)

    def available(self, branch, p):
        s = self.stock[(branch["id"], p["id"])]
        return s["on_hand"] - s["reserved"]

    def cash_move(self, session, mtype, direction, amount, at, ref_type, ref_id, description, user, **extra):
        self.add("CashMovements", Id=new_id(), CashSessionId=session["id"], Type=mtype, Direction=direction,
                 Amount=money(amount), OccurredAt=at, ReferenceType=ref_type, ReferenceId=ref_id,
                 Description=description, CreatedByUserId=user, **extra)
        if direction == 1 and mtype not in (10, 11):
            session["expected"] += money(amount)
        elif direction == 2:
            session["expected"] -= money(amount)

    # -----------------------------------------------------------------------------------------
    def weighted(self, items, key):
        return self.rng.choices(items, weights=[key(i) for i in items], k=1)[0]

    def pick_retail_lines(self, branch):
        r = self.rng
        roll = r.random()
        lines = []
        if roll < 0.05:
            pool = [self.by_code[c] for c in ("ACC-CARG10", "ACC-ARRANQ", "ACC-CABLES")]
            lines.append((r.choice(pool), 1))
        else:
            if roll < 0.12:
                pool = self.moto_batteries
            elif roll < 0.18:
                pool = self.truck_batteries
            else:
                pool = self.car_batteries
            p = self.weighted(pool, lambda x: x["w_retail"] or 0.1)
            qty = 2 if r.random() < 0.07 else 1
            lines.append((p, qty))
            if r.random() < 0.18:
                lines.append((self.by_code[r.choice(["ACC-BORNES", "ACC-AGUA"])], r.choice([1, 1, 2])))
        return [(p, q) for p, q in lines if self.available(branch, p) >= q]

    def pick_cc_lines(self, customer, branch):
        r = self.rng
        pool = self.truck_batteries + self.car_batteries if customer["truck"] else self.car_batteries + self.moto_batteries
        lines, used = [], set()
        for _ in range(r.choice([1, 1, 2, 2, 3])):
            p = self.weighted(pool, lambda x: x["w_cc"] or 0.1)
            if p["id"] in used:
                continue
            qty = r.choice([1, 2, 2, 3, 4]) if not customer["truck"] else r.choice([1, 2, 2, 4])
            if self.available(branch, p) >= qty:
                used.add(p["id"])
                lines.append((p, qty))
        return lines

    # -----------------------------------------------------------------------------------------
    def create_sale(self, branch, at, lines, *, customer=None, cc=False, status=2, session=None,
                    payments=None, trade_in=None, line_discount=0, general_discount=0, channel=None):
        sale_id = new_id()
        branch["seq"] += 1
        code = f"{branch['code']}-{branch['seq']:03d}"
        user = branch["user"]
        subtotal = D0
        details = []
        for p, qty in lines:
            total = money(Decimal(qty) * p["price"] * (Decimal(1) - Decimal(line_discount) / 100))
            subtotal += total
            details.append((p, qty, total))
            self.add("SaleDetails", SaleId=sale_id, ProductId=p["id"], Quantity=qty, UnitPrice=p["price"],
                     DiscountPercent=money(line_discount), TotalAmount=total, UnitCost=p["cost"])
        original = money(subtotal)
        total = money(subtotal * (Decimal(1) - Decimal(general_discount) / 100)) if general_discount else original
        sale = self.add("Sales", Id=sale_id, CompanyId=self.company_id, BranchId=branch["id"],
                        CustomerId=customer["id"] if customer else None,
                        CashDrawerId=branch["drawer"] if session else None,
                        CashSessionId=session["id"] if session else None, HasDelivery=False,
                        TransportAssignmentId=None, IdSaleStatus=status, NoDeliverySurchargeTotal=D0,
                        CardSurchargeTotal=D0, GeneralDiscountPercent=money(general_discount), OriginalTotal=original,
                        TotalAmount=total, ManualOverridePrice=None, OverriddenByUserId=None, OverriddenAt=None,
                        CreatedAt=at, PaidAt=at + timedelta(seconds=40) if status == 2 else None,
                        UpdatedAt=None, IsModified=False, IsCuentaCorriente=cc, SourceChannel=channel, Code=code, DeliveryAddress=None,
                        ContactPhone=customer["phone"] if customer else None, VatAmount=None, VatRate=None)

        for p, qty, _ in details:
            s = self.stock[(branch["id"], p["id"])]
            s["reserved"] += qty
            self.stock_move(s, 3, qty, "Sale", sale_id,
                            "Stock reserved for CC sale." if cc else "Stock reserved for sale.", at, user)

        trade_amount = D0
        if trade_in:
            tq, unit_value = trade_in
            trade_amount = money(unit_value * tq)
            self.add("SaleTradeIns", SaleId=sale_id, ProductId=self.casco["id"], Quantity=tq, Amount=trade_amount)
            s = self.stock[(branch["id"], self.casco["id"])]
            s["on_hand"] += tq
            self.stock_move(s, 6, tq, "Sale", sale_id,
                            "Stock received from product trade-in (CC sale)." if cc else
                            "Stock received from product trade-in.", at, user)

        if status == 2 and not cc:
            paid_at = sale["PaidAt"]
            for method, amount in payments:
                extra = {}
                if method == 3:
                    extra = dict(CardBankId=self.rng.choice(self.card_banks), CardCuotas=self.rng.choice([1, 1, 1, 3]),
                                 CardSurchargePct=D0, CardSurchargeAmt=D0, TotalCobrado=amount)
                elif method == 2:
                    extra = dict(TransferBankId=self.rng.choice(self.transfer_banks))
                self.add("SalePayments", SaleId=sale_id, Method=method, Amount=amount,
                         Reference=f"Op. {self.rng.randint(10_000_000, 99_999_999)}" if method == 2 else None, **extra)
                mtype, desc = {1: (2, "Sale payment"), 2: (10, "Pago por transferencia"), 3: (11, "Pago con tarjeta")}[method]
                self.cash_move(session, mtype, 1, amount, paid_at, "Sale", sale_id, desc, user, PaymentMethod=method)
            for p, qty, _ in details:
                s = self.stock[(branch["id"], p["id"])]
                s["reserved"] -= qty
                s["on_hand"] -= qty
                self.stock_move(s, 5, qty, "Sale", sale_id, "Stock confirmed as sold.", paid_at, user)

        info = {"id": sale_id, "row": sale, "branch": branch, "details": details, "total": total,
                "trade": trade_amount, "paid": D0, "created": at, "customer": customer}
        return info

    def cancel_sale(self, info, at):
        info["row"]["IdSaleStatus"] = 3
        info["row"]["UpdatedAt"] = at
        for p, qty, _ in info["details"]:
            s = self.stock[(info["branch"]["id"], p["id"])]
            s["reserved"] -= qty
            self.stock_move(s, 4, qty, "Sale", info["id"], "Stock reservation released.", at, info["branch"]["user"])

    # -----------------------------------------------------------------------------------------
    def retail_sale(self, branch, at, session, force_status=None):
        r = self.rng
        lines = self.pick_retail_lines(branch)
        if not lines:
            return None
        customer = r.choice(self.retail_customers) if r.random() < 0.45 else None
        channel = self.weighted(RETAIL_CHANNELS, lambda c: c[1])[0]
        battery_units = sum(q for p, q in lines if p["cat"] in (0, 1, 2))
        trade_in = None
        if battery_units and r.random() < 0.72:
            cat = lines[0][0]["cat"]
            unit = {0: 15000, 1: 25000, 2: 6000}[cat]
            trade_in = (battery_units, self.scaled(unit))

        status = force_status or (1 if r.random() < 0.03 else 2)
        if status != 2:
            return self.create_sale(branch, at, lines, customer=customer, status=status, channel=channel,
                                    trade_in=trade_in)

        roll = r.random()
        # Descuento de contado: solo en algunas ventas en efectivo.
        general_discount = 5 if roll < 0.40 and r.random() < 0.3 else 0
        # Total a cobrar (después del canje) -> se arma el split de medios de pago.
        subtotal = sum(money(Decimal(q) * p["price"]) for p, q in lines)
        total = money(subtotal * (Decimal(1) - Decimal(general_discount) / 100)) if general_discount else subtotal
        to_pay = total - (money(trade_in[1] * trade_in[0]) if trade_in else D0)
        if roll < 0.40:
            payments = [(1, to_pay)]
        elif roll < 0.74:
            payments = [(2, to_pay)]
        elif roll < 0.95:
            payments = [(3, to_pay)]
        else:
            cash_part = money(min(to_pay - 1000, Decimal(r.choice([20000, 30000, 50000]))))
            payments = [(1, cash_part), (2, to_pay - cash_part)]
        return self.create_sale(branch, at, lines, customer=customer, status=2, session=session, payments=payments,
                                trade_in=trade_in, general_discount=general_discount, channel=channel)

    def cc_sale(self, customer, at):
        r = self.rng
        branch = customer["branch"]
        lines = self.pick_cc_lines(customer, branch)
        if not lines:
            return None
        units = sum(q for _, q in lines)
        trade_in = (units, self.scaled(12000 if not customer["truck"] else 20000)) if r.random() < 0.7 else None
        info = self.create_sale(branch, at, lines, customer=customer, cc=True, status=1, trade_in=trade_in,
                                line_discount=12, channel=7)
        info["paid"] = D0
        self.cc_pending[customer["id"]].append(info)
        # Saldo a favor del cliente: se aplica solo al crear la venta (igual que CreateCcSaleHandler).
        pending = info["total"] - info["trade"]
        if customer["credit"] > 0 and pending > 0:
            applied = min(customer["credit"], pending)
            customer["credit"] -= applied
            self.add_cc_payment(info, applied, at, None, "Saldo a favor aplicado automáticamente")
        return info

    def add_cc_payment(self, info, amount, at, customer_payment_id, notes):
        info["paid"] += amount
        self.add("SaleCcPayments", Id=new_id(), SaleId=info["id"], Method=6, Amount=money(amount), Date=at,
                 Notes=notes, Status=1, CreatedAt=at, CancelledAt=None, GroupId=None,
                 CustomerPaymentId=customer_payment_id)
        if info["paid"] + info["trade"] >= info["total"]:
            info["row"]["IdSaleStatus"] = 2
            info["row"]["PaidAt"] = at
            info["row"]["UpdatedAt"] = at
            for p, qty, _ in info["details"]:
                s = self.stock[(info["branch"]["id"], p["id"])]
                s["reserved"] -= qty
                s["on_hand"] -= qty
                self.stock_move(s, 5, qty, "Sale", info["id"],
                                "Stock confirmed as sold (CC fully paid via customer payment).", at, info["branch"]["user"])
            self.cc_pending[info["customer"]["id"]].remove(info)

    def customer_payment(self, customer, at, session, day):
        r = self.rng
        pend = self.cc_pending[customer["id"]]
        # Paga lo adeudado de las ventas con más de ~5 días, redondeado como hacen los clientes reales.
        cutoff = at - timedelta(days=5)
        owed = sum((s["total"] - s["trade"] - s["paid"]) for s in pend if s["created"] <= cutoff)
        if owed <= 0:
            return
        amount = owed
        if r.random() < 0.35:
            amount = money((owed / 50000).to_integral_value(rounding="ROUND_CEILING") * 50000)
        elif r.random() < 0.15:
            amount = money((owed * Decimal("0.6") / 10000).to_integral_value() * 10000)
        if amount <= 0:
            return
        roll = r.random()
        method = 2 if roll < 0.55 else (4 if roll < 0.75 else 1)
        pid = new_id()
        cheque_id = None
        if method == 4:
            cheque_id = new_id()
            venc = day + timedelta(days=r.choice([30, 30, 45, 60]))
            estado = 3 if venc < self.today - timedelta(days=4) else (2 if venc <= self.today else 1)
            self.add("Cheques", Id=cheque_id, CompanyId=self.company_id, SalePaymentSaleId=None, SalePaymentMethod=None,
                     SaleCcPaymentId=pid, BankId=r.choice(self.cheque_banks), Numero=f"{r.randint(10_000_000, 99_999_999)}",
                     Titular=customer["name"], CuitDni=customer["cuit"], Monto=amount, FechaEmision=day,
                     FechaVencimiento=venc, Estado=estado, Notas=None, CreatedAt=at, UpdatedAt=at)
        self.add("CustomerPayments", Id=pid, CompanyId=self.company_id, CustomerId=customer["id"],
                 BranchId=session["branch"]["id"], Method=method, Amount=amount, Status=1, ChequeId=cheque_id,
                 Reference=f"Op. {r.randint(10_000_000, 99_999_999)}" if method == 2 else None, Notes=None, Date=at,
                 CreatedAt=at, CreatedByUserId=session["branch"]["user"], CardBankId=None, CardCuotas=None,
                 CardSurchargePct=None, CardSurchargeAmt=None, TotalCobrado=None,
                 TransferBankId=r.choice(self.transfer_banks) if method == 2 else None)
        mtype, direction, desc = {1: (8, 1, "Cobro cliente - efectivo"), 2: (10, 1, "Cobro cliente - transferencia"),
                                  4: (8, 3, "Cobro cliente - cheque")}[method]
        self.cash_move(session, mtype, direction, amount, at, "CustomerPayment", pid, desc, session["branch"]["user"],
                       PaymentMethod=method, CustomerPaymentId=pid)
        # Imputación FIFO; el excedente queda como saldo a favor.
        remaining = amount
        for info in sorted(list(pend), key=lambda s: s["created"]):
            if remaining <= 0:
                break
            due = info["total"] - info["trade"] - info["paid"]
            if due <= 0:
                continue
            applied = min(due, remaining)
            remaining -= applied
            self.add_cc_payment(info, applied, at, pid, None)
        customer["credit"] += remaining

    # -----------------------------------------------------------------------------------------
    def restock(self, branch, at, purchases_pending):
        """Compra semanal por proveedor para volver al stock objetivo."""
        by_supplier = defaultdict(list)
        for p in self.products:
            if p["code"] == "CASCO":
                continue
            s = self.stock[(branch["id"], p["id"])]
            avail = s["on_hand"] - s["reserved"]
            if avail < s["target"] * 0.6:
                qty = s["target"] - avail + self.rng.randint(0, max(1, s["target"] // 5))
                by_supplier[SUPPLIER_BY_BRAND[p["brand"]]].append((p, qty))
        for sup_name, lines in by_supplier.items():
            supplier = self.suppliers[sup_name]
            self.purchase_seq += 1
            pid = new_id()
            subtotal = sum(money(q * p["cost"]) for p, q in lines)
            grand = money(subtotal * Decimal("1.21"))
            row = self.add("Purchases", Id=pid, Code=f"COMP-{self.purchase_seq:04d}", CompanyId=self.company_id,
                           BranchId=branch["id"], SupplierId=supplier["id"], Status=1,
                           InvoiceNumber=f"A-0003-{self.rng.randint(10_000, 99_999):08d}", Notes=None, CreatedAt=at,
                           CreatedByUserId=self.owner_id, PaidAt=None, CancelledAt=None, IvaPct=money(21),
                           IngresosBrutosPct=None)
            for p, qty in lines:
                self.add("PurchaseDetails", Id=new_id(), PurchaseId=pid, ProductId=p["id"], ProductName=p["name"],
                         Quantity=money(qty), UnitCost=p["cost"])
                s = self.stock[(branch["id"], p["id"])]
                s["on_hand"] += qty
                self.stock_move(s, 8, qty, "Purchase", pid, "Stock received from purchase.", at, self.owner_id)
            purchases_pending[supplier["id"]].append(
                {"id": pid, "row": row, "grand": grand, "paid": D0, "created": at,
                 "pay_on": at.date() + timedelta(days=self.rng.randint(7, 15))})

    def supplier_payment(self, supplier_id, pend, at, session):
        due_now = [p for p in pend if p["pay_on"] <= (at - AR_OFFSET).date()]
        amount = sum(p["grand"] - p["paid"] for p in due_now)
        if amount <= 0:
            return
        sp_id = new_id()
        self.add("SupplierPayments", Id=sp_id, CompanyId=self.company_id, SupplierId=supplier_id,
                 BranchId=session["branch"]["id"], Method=2, Amount=amount, Status=1, ChequeId=None,
                 Reference=f"Transf. {self.rng.randint(1_000_000, 9_999_999)}", Notes=None, Date=at, CreatedAt=at,
                 CreatedByUserId=self.owner_id)
        self.cash_move(session, 12, 3, amount, at, "SupplierPayment", sp_id, "Transferencia", self.owner_id,
                       PaymentMethod=2, SupplierPaymentId=sp_id)
        for p in sorted(due_now, key=lambda x: x["created"]):
            applied = p["grand"] - p["paid"]
            p["paid"] += applied
            self.add("PurchasePayments", Id=new_id(), PurchaseId=p["id"], Method=5, Amount=applied, Status=1,
                     ChequeId=None, SupplierPaymentId=sp_id, Reference=None, Notes=None, Date=at, CreatedAt=at,
                     CreditNoteId=None)
            p["row"]["Status"] = 2
            p["row"]["PaidAt"] = at
            pend.remove(p)

    # -----------------------------------------------------------------------------------------
    def simulate(self):
        r = self.rng
        self.cc_pending = defaultdict(list)
        self.purchase_seq = 0
        purchases_pending = defaultdict(list)
        self.sessions = []
        all_retail = []
        withdrawal_reasons = ["Pago de flete", "Compra de insumos de limpieza", "Viáticos reparto",
                              "Retiro para depósito bancario", "Pago a cadete"]

        day = self.start_day
        while day <= self.today:
            if day.weekday() == 6 or day in HOLIDAYS:
                day += timedelta(days=1)
                continue
            is_today = day == self.today
            saturday = day.weekday() == 5
            close_t = time(13, 30) if saturday else time(19, 30)
            factor = MONTH_FACTOR.get(day.month, 1.0) * WEEKDAY_FACTOR[day.weekday()]
            # Leve tendencia positiva en el mes en curso, para que la comparativa luzca.
            if day.month == self.today.month and day.year == self.today.year:
                factor *= 1.08

            day_sessions = []
            for b_idx, branch in enumerate(self.branches):
                open_local = datetime.combine(day, time(8, 40 + r.randint(0, 15)))
                if is_today and open_local > self.now_local:
                    open_local = self.now_local - timedelta(minutes=5)
                close_local = datetime.combine(day, close_t) + timedelta(minutes=r.randint(0, 20))
                stays_open = False
                if is_today:
                    if b_idx == 0:
                        stays_open = True  # Casa Central: la caja de hoy sigue abierta
                    else:
                        # Sucursal Norte: cierre de turno mañana (o justo antes de ahora).
                        close_local = min(datetime.combine(day, time(13, 30)), self.now_local - timedelta(minutes=20))
                        if close_local <= open_local + timedelta(hours=1):
                            stays_open = True
                # La caja abre con lo que quedó en el cajón al cierre anterior (lo que sugiere
                # /cashsessions/last-closed); el primer día, con el fondo de cambio.
                opening = branch.get("last_close", money(FLOAT_BY_BRANCH[b_idx]))
                sid = new_id()
                session = {"id": sid, "branch": branch, "expected": D0, "open": open_local, "close": close_local,
                           "stays_open": stays_open,
                           "row": self.add("CashSessions", Id=sid, CompanyId=self.company_id, BranchId=branch["id"],
                                           CashDrawerId=branch["drawer"], OpenedByUserId=branch["user"],
                                           ClosedByUserId=None, OpenedAt=utc(open_local), ClosedAt=None,
                                           OpeningAmount=money(opening), ActualClosingAmount=None, Status=1,
                                           Notes=None)}
                self.cash_move(session, 1, 1, opening, utc(open_local), "Session", sid, "Opening float", branch["user"])
                day_sessions.append(session)
                self.sessions.append(session)

            last_local = day_sessions[0]["close"] - timedelta(minutes=15)
            if is_today:
                last_local = min(last_local, self.now_local - timedelta(minutes=8))

            def rand_time(session, start=None, end=None):
                a = start or session["open"] + timedelta(minutes=10)
                b = end or (min(session["close"], last_local) if not session["stays_open"] else last_local)
                if b <= a:
                    return None
                return a + timedelta(seconds=r.randint(0, int((b - a).total_seconds())))

            events = []
            # Compras de reposición los lunes (o primer día hábil de la semana) a primera hora.
            if day.weekday() == 0 or (day.weekday() == 1 and (day - timedelta(days=1)) in HOLIDAYS):
                for s in day_sessions:
                    events.append((s["open"] + timedelta(minutes=20), "restock", s))
            for s_idx, session in enumerate(day_sessions):
                mean = (7.5 if s_idx == 0 else 4.5) * factor * self.volume_factor
                n = max(1, round(r.gauss(mean, mean * 0.25)))
                for _ in range(n):
                    t = rand_time(session)
                    if t:
                        events.append((t, "retail", session))
            n_cc = max(0, round(r.gauss(2.3 * factor * self.volume_factor, 0.8)))
            for _ in range(n_cc):
                customer = self.weighted(self.wholesale, lambda c: c["weight"])
                session = day_sessions[0] if customer["branch"] is self.branches[0] else day_sessions[1]
                t = rand_time(session)
                if t:
                    events.append((t, "cc", customer))
            for customer in self.wholesale:
                if customer["next_payment"] <= day:
                    session = day_sessions[0] if customer["branch"] is self.branches[0] else day_sessions[1]
                    t = rand_time(session)
                    if t:
                        events.append((t, "cobro", (customer, session)))
                        customer["next_payment"] = day + timedelta(days=customer["every"] + r.randint(-1, 2))
            for supplier_id, pend in purchases_pending.items():
                if any(p["pay_on"] <= day for p in pend):
                    t = rand_time(day_sessions[0], end=datetime.combine(day, time(12, 0)))
                    if t:
                        events.append((t, "supplier", (supplier_id, pend)))
            if r.random() < 0.25:
                t = rand_time(day_sessions[0])
                if t:
                    events.append((t, "withdrawal", day_sessions[0]))
            if is_today:
                # Garantías para el pulso del día: una pendiente y una cancelada.
                t = rand_time(day_sessions[0])
                if t:
                    events.append((t, "pending", day_sessions[0]))
                    events.append((t + timedelta(minutes=3), "cancelled", day_sessions[0]))

            for t, kind, payload in sorted(events, key=lambda e: e[0]):
                at = utc(t)
                if kind == "restock":
                    self.restock(payload["branch"], at, purchases_pending)
                elif kind == "retail":
                    info = self.retail_sale(payload["branch"], at, payload)
                    if info:
                        all_retail.append(info)
                        # Algunas pendientes viejas se cancelan; las de los últimos días quedan pendientes.
                        if info["row"]["IdSaleStatus"] == 1 and day < self.today - timedelta(days=10):
                            self.cancel_sale(info, at + timedelta(hours=r.randint(1, 30)))
                elif kind == "cc":
                    self.cc_sale(payload, at)
                elif kind == "cobro":
                    customer, session = payload
                    self.customer_payment(customer, at, session, day)
                elif kind == "supplier":
                    supplier_id, pend = payload
                    self.supplier_payment(supplier_id, pend, at, day_sessions[0])
                elif kind == "withdrawal":
                    session = payload
                    amount = money(r.choice([15000, 20000, 25000, 30000, 40000, 60000]))
                    if session["expected"] - amount > 30000:
                        self.cash_move(session, 3, 2, amount, at, "Withdrawal", None, r.choice(withdrawal_reasons),
                                       self.owner_id)
                elif kind == "pending":
                    self.retail_sale(payload["branch"], at, payload, force_status=1)
                elif kind == "cancelled":
                    info = self.retail_sale(payload["branch"], at, payload, force_status=1)
                    if info:
                        self.cancel_sale(info, at + timedelta(minutes=12))

            # Cierre de caja: retiro a tesorería dejando el fondo de cambio, y arqueo.
            for b_idx, session in enumerate(day_sessions):
                if session["stays_open"]:
                    continue
                keep = money(FLOAT_BY_BRANCH[b_idx] + r.choice([0, 0, 2000, 3500, 5000, 7800]))
                withdrawal = money(((session["expected"] - keep) / 1000).to_integral_value(rounding="ROUND_FLOOR") * 1000)
                if withdrawal > 0:
                    self.cash_move(session, 3, 2, withdrawal, utc(session["close"] - timedelta(minutes=5)), "Withdrawal",
                                   None, "Retiro a tesorería (cierre)", session["branch"]["user"])
                roll = r.random()
                diff, note = D0, None
                if roll < 0.12:
                    diff = -money(r.randint(2, 25) * 100)
                    note = "Faltante en arqueo"
                elif roll < 0.20:
                    diff = money(r.randint(1, 10) * 100)
                    note = "Sobrante de cambio"
                if is_today and session["branch"] is self.branches[1]:
                    note = "Cierre turno mañana"
                row = session["row"]
                row["Status"] = 2
                row["ClosedAt"] = utc(session["close"])
                row["ClosedByUserId"] = session["branch"]["user"]
                row["ActualClosingAmount"] = money(session["expected"] + diff)
                row["Notes"] = note
                session["branch"]["last_close"] = row["ActualClosingAmount"]
            day += timedelta(days=1)

        self.final_state()

    def final_state(self):
        for s in self.stock.values():
            assert s["on_hand"] >= 0 and s["reserved"] >= 0 and s["reserved"] <= s["on_hand"] + 1000, s
            self.add("BranchProductStocks", Id=s["id"], CompanyId=self.company_id, BranchId=s["branch"]["id"],
                     ProductId=s["product"]["id"], OnHandQuantity=s["on_hand"], ReservedQuantity=s["reserved"],
                     UpdatedAt=self.now_utc, CostOverride=None, SalePriceOverride=None)
        for c in self.wholesale:
            c["row"]["CreditBalance"] = money(c["credit"])
        self.build_quotes()

    def build_quotes(self):
        r = self.rng
        start = self.now_local - timedelta(days=25)
        for i in range(18):
            branch = self.branches[0] if r.random() < 0.7 else self.branches[1]
            branch["quote_seq"] += 1
            created_local = start + timedelta(days=i * 1.35, hours=r.randint(0, 6))
            if created_local > self.now_local - timedelta(hours=1):
                created_local = self.now_local - timedelta(hours=2 + i)
            lines = []
            for p in r.sample(self.car_batteries + self.truck_batteries, r.choice([1, 1, 2, 3])):
                lines.append((p, r.choice([1, 2, 4, 6, 10])))
            discount = r.choice([0, 0, 5, 8, 10])
            qid = new_id()
            total = D0
            for p, q in lines:
                lt = money(Decimal(q) * p["price"] * (Decimal(1) - Decimal(discount) / 100))
                total += lt
                self.add("QuoteDetails", QuoteId=qid, ProductId=p["id"], Quantity=q, UnitPrice=p["price"],
                         DiscountPercent=money(discount), LineTotal=lt)
            prospect = r.random() < 0.5
            customer = None if prospect else r.choice(self.wholesale + self.retail_customers)
            status = 3 if i % 6 == 0 else 1
            self.add("Quotes", Id=qid, CompanyId=self.company_id, BranchId=branch["id"],
                     CustomerId=customer["id"] if customer else None,
                     ProspectName=r.choice(["Flota Logística Oeste", "Cooperativa de Taxis Morón", "Hugo Benítez",
                                            "Taller El Tano", "Club Atlético Haedo", "Remisería La Estación"])
                     if prospect else None,
                     ProspectContact=f"11 {r.randint(3000, 6999)}-{r.randint(1000, 9999)}" if prospect else None,
                     GeneralDiscountPercent=D0, TotalAmount=money(total),
                     ExpiresAt=utc(created_local + timedelta(days=15)), IdQuoteStatus=status, ConvertedSaleId=None,
                     Code=f"PRES-{branch['code']}-{branch['quote_seq']:03d}", CreatedByUserId=branch["user"],
                     CreatedAt=utc(created_local), IncludesVat=False, VatRate=money(21))

    # -----------------------------------------------------------------------------------------
    ORDER = ["FiscalPointsOfSale", "Branches", "BankInstallmentPlans", "Users", "UserBranchAccesses", "CashDrawers",
             "CashDrawerUserAssignments", "ProductCategories", "Products", "Customers", "Suppliers",
             "BranchProductStocks", "CashSessions", "Sales", "SaleDetails", "SalePayments", "SaleTradeIns",
             "CustomerPayments", "Cheques", "SaleCcPayments", "CashMovements", "StockMovements", "Purchases",
             "PurchaseDetails", "SupplierPayments", "PurchasePayments", "Quotes", "QuoteDetails"]

    def flush(self):
        assert set(self.rows) <= set(self.ORDER), set(self.rows) - set(self.ORDER)
        for table in self.ORDER:
            rows = self.rows.get(table)
            if not rows:
                continue
            cols = sorted({k for row in rows for k in row})
            values = [tuple(row.get(c) for c in cols) for row in rows]
            col_sql = ",".join(f'"{c}"' for c in cols)
            psycopg2.extras.execute_values(self.cur, f'insert into "{table}" ({col_sql}) values %s', values,
                                           page_size=1000)
        self.cur.execute(
            'update "CompanyOnboarding" set "HasCreatedBranch"=true, "HasCreatedCashDrawer"=true, '
            '"HasCompletedInitialCashOpen"=true, "HasCreatedProduct"=true, "HasLoadedInitialStock"=true, '
            '"CompletedAt"=%s, "UpdatedAt"=%s where "CompanyId"=%s',
            (self.master_created, self.master_created, self.company_id))

    def summary(self):
        sales = self.rows["Sales"]
        month_start = self.today.replace(day=1)
        def local_day(row):
            return (row["CreatedAt"] - AR_OFFSET).date()
        this_month = [s for s in sales if local_day(s) >= month_start and s["IdSaleStatus"] != 3]
        today = [s for s in sales if local_day(s) == self.today]
        open_sessions = [s for s in self.rows["CashSessions"] if s["Status"] == 1]
        print(f"Empresa {self.company_id}")
        print(f"  Período simulado: {self.start_day} -> {self.today} ({len(self.rows['CashSessions'])} sesiones de caja, "
              f"{len(open_sessions)} abierta/s)")
        print(f"  Ventas: {len(sales)} total | mes en curso {len(this_month)} por "
              f"${sum(s['TotalAmount'] for s in this_month):,.0f} | hoy {len(today)} "
              f"(pagadas {sum(1 for s in today if s['IdSaleStatus'] == 2)}, "
              f"pendientes {sum(1 for s in today if s['IdSaleStatus'] == 1)}, "
              f"canceladas {sum(1 for s in today if s['IdSaleStatus'] == 3)})")
        cc_debt = sum(sum(i['total'] - i['trade'] - i['paid'] for i in v) for v in self.cc_pending.values())
        print(f"  Cuentas corrientes: deuda pendiente ${cc_debt:,.0f} en "
              f"{sum(len(v) for v in self.cc_pending.values())} ventas")
        print(f"  Compras: {len(self.rows['Purchases'])} | impagas "
              f"{sum(1 for p in self.rows['Purchases'] if p['Status'] == 1)}")
        print(f"  Movimientos de caja: {len(self.rows['CashMovements'])} | de stock: {len(self.rows['StockMovements'])}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--conn", default=os.environ.get("EITI_DEMO_CONN", DEFAULT_CONN))
    parser.add_argument("--owner", default="demo", help="username del owner de la empresa demo")
    parser.add_argument("--days", type=int, default=105, help="días de historia a generar")
    parser.add_argument("--seed", type=int, default=20261015, help="semilla del generador aleatorio")
    parser.add_argument("--price-factor", type=float, default=0.8, help="escala de precios/importes")
    parser.add_argument("--volume-factor", type=float, default=0.75, help="escala de cantidad de ventas")
    args = parser.parse_args()

    conn = psycopg2.connect(args.conn)
    try:
        seeder = Seeder(conn, args.owner, args.days, args.seed, args.price_factor, args.volume_factor)
        seeder.wipe()
        seeder.build_master_data()
        seeder.simulate()
        seeder.flush()
        conn.commit()
        seeder.summary()
        print(f"\nLogins (password {DEMO_PASSWORD} para cajero.centro / vendedor.norte): "
              f"{args.owner}, cajero.centro, vendedor.norte")
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


if __name__ == "__main__":
    main()
