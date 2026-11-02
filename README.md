# PrensE-commerce · Backend

Web API en **.NET 9** para la tienda PrensE-commerce (frontend Angular 19 en
[`frontendprensecommerce`](https://github.com/wprensh/frontendprensecommerce)).

- **Vertical Slice Architecture**: el código se organiza por casos de uso (`Features/<Feature>/<UseCase>`), no por capas técnicas.
- **SOLID**: cada slice tiene su endpoint, su handler y su validador; las piezas transversales se consumen por interfaces.
- **Multi-tenant**: una sola base de datos PostgreSQL con **discriminador `tenant_id`** y filtros globales de EF Core.
- **PostgreSQL** (EF Core 9 + Npgsql) y **Redis** (HybridCache + carrito).

## Estructura

```text
backendprensecommerce/
├── PrensEcommerce.sln
├── Directory.Build.props            # net9.0, nullable, analizadores, warnings = errores
├── docker-compose.yml               # PostgreSQL 17 + Redis 7 para desarrollo
├── database/schema.sql              # script SQL idempotente generado desde las migraciones
├── .config/dotnet-tools.json        # dotnet-ef 9
├── src/PrensEcommerce.Api/
│   ├── Program.cs                   # composición: Postgres, Redis, tenant, JWT, slices
│   ├── Features/                    # ← SLICES (un caso de uso por carpeta)
│   │   ├── Auth/
│   │   │   ├── AuthResponse.cs      # contrato compartido de la feature
│   │   │   ├── Login/Login.cs       # Command + Validator + Endpoint + Handler
│   │   │   └── Register/Register.cs
│   │   ├── Catalog/
│   │   │   ├── ProductResponse.cs
│   │   │   ├── GetProducts/GetProducts.cs
│   │   │   ├── GetProductById/GetProductById.cs
│   │   │   └── CreateProduct/CreateProduct.cs
│   │   ├── Cart/
│   │   │   ├── CartStore.cs         # carrito en Redis, aislado por tenant
│   │   │   ├── GetCart/GetCart.cs
│   │   │   ├── AddCartItem/AddCartItem.cs
│   │   │   └── RemoveCartItem/RemoveCartItem.cs
│   │   ├── Checkout/
│   │   │   └── PlaceOrder/PlaceOrder.cs
│   │   └── Tenants/
│   │       └── GetCurrentTenant/GetCurrentTenant.cs
│   ├── Domain/                      # entidades compartidas por varios slices (un archivo por módulo)
│   │   ├── Entity.cs                # Entity, ITenantEntity, TenantEntity
│   │   ├── Tenant.cs  Identity.cs  Catalog.cs  Sales.cs  Support.cs  Crm.cs
│   ├── Infrastructure/
│   │   ├── MultiTenancy/            # ITenantProvider, resolvers, middleware, store
│   │   └── Persistence/             # AppDbContext, configuraciones, migraciones, seeder
│   └── Common/                      # piezas transversales sin lógica de negocio
│       ├── Abstractions/            # IEndpoint, IHandler
│       ├── Endpoints/               # auto-registro de slices
│       ├── Caching/                 # ITenantCache (Redis con prefijo por tenant)
│       ├── Errors/                  # Result<T>, Error, GlobalExceptionHandler
│       ├── Security/                # JWT (claim tenant_id)
│       └── Validation/              # filtro FluentValidation
└── tests/PrensEcommerce.Api.Tests/  # resolvers, TenantProvider y middleware
```

Añadir un caso de uso = crear `Features/<Feature>/<UseCase>/<UseCase>.cs` con una clase
`Endpoint : IEndpoint` y un `Handler : IHandler`. Se registran solos; `Program.cs` no cambia.

## Multi-tenancy

**Decisión: base de datos única, esquema compartido, discriminador `tenant_id`.**

| | Discriminador (elegido) | Esquema por tenant |
|---|---|---|
| Migraciones | Una sola vez | Una por esquema y por tenant |
| Alta de tienda | `INSERT` en `tenants` | `CREATE SCHEMA` + migrar |
| Modelo EF | Un modelo cacheado | Un modelo por esquema (`IModelCacheKeyFactory`) |
| Conexiones (Neon/pooler) | Un pool | Pool compartido con `search_path` dinámico |
| Aislamiento | Lógico (filtro global + validaciones) | Más fuerte |

Para un e-commerce con muchas tiendas pequeñas, el discriminador escala mejor y es más simple
de operar. El aislamiento se refuerza en varias capas:

1. **Filtro global** en `AppDbContext`: toda entidad `ITenantEntity` se consulta con `WHERE tenant_id = @tenant`.
2. **Escritura**: `SaveChanges` asigna `TenantId` desde `ITenantProvider` (nunca del cliente) y bloquea cambios entre tenants.
3. **FK** `tenant_id → tenants.id` en todas las tablas.
4. **JWT**: el token lleva `tenant_id`; si no coincide con el tenant resuelto → `403`.
5. **Redis**: claves con prefijo `t:{tenantId}:`.

Siguiente paso recomendado para defensa en profundidad: Row-Level Security de PostgreSQL.

### Esquemas de la base de datos

Una sola base `prens_ecommerce`, con un esquema de PostgreSQL por módulo. Todas las tablas
(excepto `core.tenants`) llevan `tenant_id`.

| Esquema | Tablas | Módulo |
|---|---|---|
| `core` | `tenants`, `__ef_migrations_history` | Tiendas |
| `identity` | `users`, `roles`, `user_roles` | Usuarios y roles (permisos en `text[]`) |
| `catalog` | `categories`, `products` | Catálogo y mantenimiento de productos |
| `sales` | `orders`, `order_items`, `payments` | Checkout y pagos |
| `support` | `tickets`, `ticket_messages` | Atención al cliente |
| `crm` | `loyalty_accounts`, `loyalty_transactions`, `customer_notes`, `automation_rules`, `automation_runs` | Fidelización, CRM y automatizaciones |

Roles de sistema por tienda: `Admin`, `CatalogManager`, `CustomerService`, `Marketing`, `Customer`.
El JWT incluye un claim `role` por rol; `POST /api/products` exige `Admin` o `CatalogManager`.

En `Development`, la tienda `demo` incluye un administrador **`admin@prens.shop` / `Admin123!`**
(solo para desarrollo local).

### Resolución del tenant

`TenantResolutionMiddleware` prueba los `ITenantResolver` registrados, en orden:

1. Header **`X-Tenant-ID`** (slug o Guid) → útil para el frontend en `localhost` y para pruebas.
2. **Subdominio**: `acme.prens.shop` → `acme` (solo bajo `MultiTenancy:RootDomains`; `www`, `api`, `admin`, `app` reservados).

Después busca el tenant en `ITenantStore` (PostgreSQL con caché HybridCache/Redis) y lo fija en el
`TenantProvider` **Scoped**, expuesto como `ITenantProvider` (lectura) e `ITenantSetter` (escritura).

| Caso | Respuesta |
|---|---|
| Sin header ni subdominio | `400 Tenant requerido` |
| Tenant inexistente o inactivo | `404 Tenant no encontrado` |
| Token de otra tienda | `403 Tenant no válido` |
| `/health`, `/openapi` | Sin tenant |

## Ejecutar en local

Requisitos: .NET 9 SDK y Docker.

```bash
docker compose up -d
dotnet tool restore
dotnet run --project src/PrensEcommerce.Api --launch-profile http
```

En `Development` se aplican las migraciones y se crea la tienda **`demo`** con productos.

```bash
curl -H "X-Tenant-ID: demo" http://localhost:5080/api/products
curl http://demo.localhost:5080/api/tenant          # resolución por subdominio
```

### Endpoints

| Método | Ruta | Auth |
|---|---|---|
| GET | `/api/tenant` | – |
| GET | `/api/products?category=&search=&sort=&page=&pageSize=` | – |
| GET | `/api/products/{id}` | – |
| POST | `/api/products` | JWT (Admin, CatalogManager) |
| POST | `/api/auth/register` · `/api/auth/login` | – |
| GET | `/api/cart` | JWT |
| POST | `/api/cart/items` | JWT |
| DELETE | `/api/cart/items/{productId}` | JWT |
| POST | `/api/checkout` | JWT |
| GET | `/health` | – |

`sort` acepta los valores del frontend: `relevance`, `price-asc`, `price-desc`, `rating`.

### Atención al cliente y fidelización

**Soporte** (`Features/Support`, esquema `support`)

| Método | Ruta | Quién |
|---|---|---|
| POST | `/api/support/tickets` | Cliente: abre un caso (asunto, categoría, mensaje, pedido opcional) |
| GET | `/api/support/tickets` · `/api/support/tickets/{id}` | Cliente: sus casos y la conversación (sin notas internas) |
| POST | `/api/support/tickets/{id}/messages` | Cliente: responde (el caso vuelve a `open`) |
| POST | `/api/support/tickets/{id}/rating` | Cliente: valora 1–5 un caso resuelto |
| GET | `/api/admin/support/tickets?status=&priority=&search=` | Agente: bandeja con contadores y nota media |
| GET/PATCH | `/api/admin/support/tickets/{id}` | Agente: detalle; cambia estado/prioridad |
| POST | `/api/admin/support/tickets/{id}/messages` | Agente: responde (`waitingCustomer`) o nota interna (`isInternal: true`) |

Cada caso tiene un número correlativo por tienda (`TK-000001`). Pagos y devoluciones entran con prioridad alta.

**Fidelización** (`Features/Loyalty`, esquema `crm`)

| Método | Ruta | Quién |
|---|---|---|
| GET | `/api/loyalty/me` | Cliente: saldo, nivel, progreso, movimientos y cupones activos |
| POST | `/api/loyalty/redeem` | Cliente: canjea puntos por un cupón `PR-XXXXXXXX` |
| POST | `/api/admin/loyalty/adjustments` | Admin/Marketing: bonificación o corrección manual |

Reglas por defecto (sección `Loyalty` de appsettings): 1 punto por euro, 100 puntos = 1 €, canje mínimo 500,
cupones válidos 90 días. Niveles por puntos históricos: Bronce 0, Plata 1 000 (×1,25), Oro 5 000 (×1,5),
Platino 15 000 (×2). El checkout (`POST /api/checkout`) acepta `voucherCode` y devuelve `loyaltyDiscount` y `pointsEarned`.

Políticas: `support-agent` (rol Admin o CustomerService, o permiso `support.write`) y `loyalty-manager`
(rol Admin o Marketing, o permiso `crm.write`).

### Migraciones

Sin .NET, puedes crear todo con el script: `psql -d prens_ecommerce -f database/schema.sql`.

```bash
dotnet ef migrations add <Nombre> -p src/PrensEcommerce.Api -o Infrastructure/Persistence/Migrations
dotnet ef database update -p src/PrensEcommerce.Api
```

## Configuración (producción)

Usa variables de entorno (no subas secretos al repositorio):

| Variable | Ejemplo |
|---|---|
| `ConnectionStrings__Postgres` | `Host=...neon.tech;Database=...;Username=...;Password=...;SSL Mode=Require` |
| `ConnectionStrings__Redis` | `host:6379,password=...,ssl=True,abortConnect=False` |
| `Jwt__SigningKey` | clave aleatoria de 32+ caracteres |
| `MultiTenancy__RootDomains__0` | `prens.shop` |
| `Cors__AllowedOrigins__0` | `https://*.prens.shop` |
