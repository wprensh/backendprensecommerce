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
│   ├── Domain/                      # entidades compartidas por varios slices
│   │   ├── Entity.cs                # Entity, ITenantEntity, TenantEntity
│   │   ├── Tenant.cs  Product.cs  User.cs  Order.cs
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
| POST | `/api/products` | JWT |
| POST | `/api/auth/register` · `/api/auth/login` | – |
| GET | `/api/cart` | JWT |
| POST | `/api/cart/items` | JWT |
| DELETE | `/api/cart/items/{productId}` | JWT |
| POST | `/api/checkout` | JWT |
| GET | `/health` | – |

`sort` acepta los valores del frontend: `relevance`, `price-asc`, `price-desc`, `rating`.

### Migraciones

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
