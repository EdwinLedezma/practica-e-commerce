# Decisiones de arquitectura

## Tres procesos independientes

Catalog.Api (5101) e Inventory.Api (5102) poseen sus propios datos de demostración. Gateway.Api (5100) los consume por HTTP, consulta Frankfurter y sirve la interfaz. El gateway no referencia proyectos del catálogo ni del inventario. Los repositorios en memoria son suficientes para esta práctica de lectura; los datos se reinician con cada proceso. No hay CRUD, compras ni pagos en este alcance.

## Clean Architecture y SOLID

Cada servicio tiene Domain, Application, Infrastructure y Api. Domain contiene datos e invariantes de su contexto. Application contiene consultas o la orquestación, y declara los puertos de acceso a datos y servicios externos. Infrastructure implementa esos puertos. Api solo configura dependencias y traduce HTTP a casos de uso. Las referencias apuntan hacia las capas internas; únicamente la raíz de composición Api conoce las implementaciones.

- Responsabilidad única: consulta, almacenamiento de demostración, transporte HTTP y presentación están separados.
- Abierto a extensión: una nueva implementación de repositorio o cliente sustituye el adaptador mediante DI.
- Sustitución: los dobles de prueba implementan los mismos contratos que los clientes reales.
- Segregación de interfaces: cada cliente expone una sola operación; no existe un cliente genérico de negocio con métodos ajenos.
- Inversión de dependencias: ProductSummaryService recibe IProductClient, IInventoryClient e IExchangeRateClient; no crea HttpClient ni conoce URLs.

No se agregan repositorios genéricos, mediadores, paquetes de resiliencia ni una base de datos para justificar las capas. El dominio del gateway contiene la conversión monetaria; los adaptadores validan los contratos que reciben de otros procesos.

## Orquestación y contrato

Las tres tareas se inician antes de Task.WhenAll. El tiempo de red queda dominado por la dependencia más lenta, con sobrecosto de orquestación. Esto no implica que siempre sea tres veces más rápido: la mejora depende de latencia, conexiones y cargas reales. Se reduce de tres a una la cantidad de solicitudes del cliente para obtener el detalle; el gateway sigue realizando tres consultas salientes.

Ruta canónica: GET /api/v1/products/{id}/summary. Alias del enunciado: GET /api/v1/product-summary/{id}. El JSON final usa snake_case. Los microservicios internos usan camelCase. Los identificadores aceptados tienen el formato P- seguido por tres dígitos. P-999 sirve para comprobar el recurso inexistente.

USD y MXN se procesan con decimal. El precio convertido se redondea a dos decimales con MidpointRounding.AwayFromZero. Cada dependencia informa su duración medida en el servidor; el dashboard mide también la duración completa percibida por el cliente. Esas medidas incluyen transporte y no equivalen a tiempo de CPU.

## Fallos y cancelación

HttpClient se obtiene por AddHttpClient. Cada consulta tiene un límite configurable de cuatro segundos que incluye lectura y deserialización del cuerpo, además del token de cancelación del cliente. No se realizan reintentos automáticos: la interfaz permite una nueva consulta explícita. No se usan tasas fijas o respuestas simuladas como fallback de producción.

| Situación | HTTP | Resultado |
| --- | --- | --- |
| Todo disponible | 200 | SUCCESS |
| Inventario o divisas fallan | 200 | PARTIAL; campos afectados null |
| Producto inexistente | 404 | Problem Details |
| Identificador inválido | 400 | Problem Details |
| Catálogo devuelve error, JSON inválido o conexión rechazada | 502 | Problem Details |
| Catálogo excede el tiempo de espera | 504 | Problem Details |

La caída de inventario produce UNKNOWN, no OUT_OF_STOCK. Solo un stock confirmado igual a cero produce OUT_OF_STOCK. No se promete entrega si no hay stock confirmado. Una tasa inválida o un contrato incompleto se rechazan. La cancelación de la solicitud se propaga; no se confunde con una respuesta parcial. Los errores inesperados se procesan con el manejador de excepciones de ASP.NET Core sin revelar trazas al cliente.

## Divisas y seguridad

Se usa https://api.frankfurter.dev/v2/rate/USD/MXN, sin clave. El proveedor publica tasas de referencia diarias, no cotizaciones intradía. La fecha original se conserva en exchange_rate_date. Consultar https://frankfurter.dev/ para condiciones y documentación.

Las URLs solo se configuran en el servidor. El cliente no puede elegir destinos arbitrarios. El frontend utiliza textContent para datos externos y una política CSP del mismo origen. La demostración usa HTTP exclusivamente en loopback y HTTPS para Frankfurter. Un despliegue público requeriría HTTPS, configuración operativa y controles apropiados a su entorno. No se implementa autenticación porque los datos de esta práctica son de lectura y demostración.
