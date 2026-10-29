# Práctica e-commerce

Aplicación individual de **Composite API y consumo de microservicios**, Desarrollo Web Integral, Unidad 3, UTCH. Autor: Edwin Arturo Ledezma Rivas.

El dashboard consulta una sola ruta. El gateway reúne catálogo, inventario y una tasa USD/MXN real de Frankfurter mediante tres llamadas HTTP concurrentes. La solución utiliza C#, ASP.NET Core .NET 10, Clean Architecture, inyección de dependencias y JavaScript sin frameworks. No requiere paquetes NuGet externos.

## Ejecutar en Windows

Requisito: SDK de .NET 10 y acceso a Internet para la consulta real de divisas.

```powershell
git clone https://github.com/EdwinLedezma/practica-e-commerce.git
cd practica-e-commerce
git switch feature/composite-api-dashboard
dotnet build ECommerce.slnx
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start.ps1
```

Abrir http://localhost:5100. Ctrl+C detiene los tres procesos iniciados por el script. El repositorio se creó privado: se requiere acceso a la cuenta o una invitación para clonarlo y revisarlo. El trabajo completo permanece en la rama feature hasta integrar el PR.

Si un puerto está ocupado, el script acepta `-GatewayPort 5200 -CatalogPort 5201 -InventoryPort 5202`.

También pueden ejecutarse en tres terminales (Windows, Linux o macOS):

```text
dotnet run --project src/Catalog.Api --urls http://localhost:5101
dotnet run --project src/Inventory.Api --urls http://localhost:5102
dotnet run --project src/Gateway.Api --urls http://localhost:5100
```

## Probar

```powershell
dotnet build ECommerce.slnx
dotnet run --project tests/ECommerce.Tests --no-build
Invoke-RestMethod http://localhost:5100/api/v1/products/P-101/summary
```

Las verificaciones son un ejecutable sin paquetes externos; devuelven código distinto de cero al fallar. Inician servicios reales en puertos disponibles y un servidor local que sustituye exclusivamente a Frankfurter durante las pruebas reproducibles. Cierran sus propios procesos. No usan `dotnet test`. El registro de ejecución está en [docs/test-results.txt](docs/test-results.txt). La prueba automatizada no depende de Internet; las consultas reales se conservan por separado en `docs/evidence/`.

| Servicio | Endpoint | Puerto |
| --- | --- | --- |
| Catálogo propio | `/microservices/products/{id}` | 5101 |
| Inventario propio | `/microservices/inventory/{id}` | 5102 |
| Frankfurter externo | `https://api.frankfurter.dev/v2/rate/USD/MXN` | HTTPS |
| Composite y dashboard | `/api/v1/products/{id}/summary` y `/` | 5100 |

Alias admitido: `/api/v1/product-summary/{id}`. Cada API propia expone `/health`. Los servicios internos pueden consultarse individualmente. [docs/requests.http](docs/requests.http) contiene solicitudes para un cliente REST; pueden copiarse en Postman o Insomnia.

Productos de demostración: P-101 (laptop, 15 unidades), P-102 (audífonos, 4 unidades), P-103 (teclado, sin stock). P-999 devuelve 404. Un ID sin formato válido devuelve 400. El dashboard permite seleccionar los cuatro casos, reintentar, ver y copiar JSON.

## Configuración

Variables de entorno del gateway, equivalentes a las claves de configuración ASP.NET Core:

| Variable | Valor predeterminado |
| --- | --- |
| `Services__CatalogUrl` | `http://localhost:5101/` |
| `Services__InventoryUrl` | `http://localhost:5102/` |
| `Services__CurrencyUrl` | `https://api.frankfurter.dev/` |
| `Services__TimeoutSeconds` | `4` (rango admitido 0.1–30) |

Las URLs base deben terminar con `/`. Frankfurter entrega tasas diarias; el JSON incluye la fecha de referencia. No se promete una cotización intradía ni se conserva una tasa fija de respaldo.

Para comprobar resiliencia manualmente, iniciar catálogo y gateway dejando inventario detenido: la consulta conserva el producto y devuelve PARTIAL, stock UNKNOWN y unidades null. Si se bloquea Frankfurter, price_mxn es null. Si catálogo falla, el gateway devuelve 502 o 504 según sea error o timeout. Nunca se sustituye un fallo por un stock o precio inventado.

## Estructura y decisiones

```text
src/
  Catalog.{Domain,Application,Infrastructure,Api}/
  Inventory.{Domain,Application,Infrastructure,Api}/
  Gateway.{Domain,Application,Infrastructure,Api}/
    Gateway.Api/wwwroot/       dashboard del mismo origen
tests/ECommerce.Tests/         dominio, concurrencia, contratos y HTTP
scripts/start.ps1              inicia y detiene los tres procesos
docs/                         reporte, solicitudes y evidencia
```

Cada servicio se despliega y ejecuta de forma independiente. El gateway depende solo de contratos HTTP, no de los ensamblados de catálogo o inventario. Los datos propios son inmutables y de demostración, almacenados en memoria. Las dependencias, SOLID, manejo de fallos y límites se explican en [docs/architecture.md](docs/architecture.md).

## Flujo Git

```text
main → develop → feature/composite-api-dashboard
feature/composite-api-dashboard → PR a develop → merge commit
develop → PR a main → squash merge
```

Se preservan commits descriptivos y PR para documentar la práctica individual. El workflow `.github/workflows/ci.yml` compila y ejecuta las verificaciones en pushes y PR. No se simulan revisiones ni aprobaciones de otra persona. Antes de integrar, comprobar el resultado de CI y revisar el diff. Después de una promoción por squash, comparar historiales antes de la siguiente entrega.

## Reporte y evidencia

El reporte Word y PDF se guarda en `docs/report/`. Las respuestas reales incluyen URL, HTTP, fecha UTC, duración y cuerpo en `docs/evidence/`. La sección de evidencias identifica cualquier captura visual pendiente; esos registros no se presentan como capturas de Postman o del navegador. El grupo no fue proporcionado y figura como pendiente de completar en la portada.

Fuente del servicio externo: [documentación oficial de Frankfurter](https://frankfurter.dev/).
