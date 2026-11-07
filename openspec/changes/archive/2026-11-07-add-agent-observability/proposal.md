# Proposal

## Why

Hoy todo lo que el agente cuenta de sí mismo son líneas de registro planas por la salida de errores: el modelo, los tokens y la duración de cada llamada, y la auditoría en `Sandbox.Audit` con su identificador de ejecución. Eso basta para saber *qué* pasó, pero no *cuándo ni dentro de qué*: qué llamada al modelo pidió cada tool, cuánto tardó la persona en aprobar una cancelación o qué vuelta consumió los tokens. Para depurar y explicar un agente hace falta la estructura temporal de una **traza**, y el proyecto quiere aprender a obtenerla con el estándar del sector, OpenTelemetry, y verla en un panel.

Este change es el tercero y último de la **fase 5** de `docs/plan.md` (*Guardrails*): "panel de trazas OTLP opcional (Aspire Dashboard como perfil de compose)". El explore hizo un spike con `Microsoft.Extensions.AI` 10.11.0, OpenTelemetry 1.20.1 y el panel 13.6.2 (detalle en `design.md`). Estos hallazgos condicionan el alcance:

- `OpenTelemetryChatClient` copia el **mensaje crudo de la excepción** del proveedor en el estado del span. Si quedara por dentro del traductor de errores, el panel mostraría el fragmento de la API key que DeepSeek devuelve en el 401.
- Sin un **span raíz propio**, una ejecución con aprobación humana se parte en dos trazas, con la aprobación suelta.
- Con el endpoint configurado y nadie escuchando, el Runner tarda entre **8,7 y 11,9 s más en salir**, sin ningún aviso. La telemetría debe estar apagada mientras nadie pida lo contrario.
- La captura de contenido de las conversaciones no tiene interruptor estándar en la 10.11.0: hace falta una opción propia.
- El panel funciona con el endurecimiento del proyecto, salvo que necesita un `/tmp` escribible.

## What Changes

- **Trazas del agente** (`Sandbox.Agents`, sin paquetes nuevos: `ActivitySource` es de .NET):
  - cada ejecución del agente abre un span raíz `invoke_agent sales-agent` con el identificador de ejecución, el motivo de parada, las llamadas y los tokens;
  - cuelgan de él un span por llamada al modelo (`chat`, convenciones GenAI de `Microsoft.Extensions.AI`), uno por tool ejecutada (`execute_tool`) y uno por confirmación humana (`approval`), con su decisión;
  - el cliente de OpenTelemetry va **por fuera** del traductor de errores del proveedor, para que ninguna traza lleve el texto de error del proveedor.
- **Captura de contenido opcional**: `Telemetry:CaptureContent`, desactivada por defecto. Activada, los spans de llamada al modelo incluyen los mensajes, las definiciones de las tools, sus argumentos y sus resultados, y el Runner avisa al arrancar. Nunca llega a los registros de consola.
- **Exportación OTLP en el Runner**:
  - solo si `OTEL_EXPORTER_OTLP_ENDPOINT` tiene valor: trazas, logs (con *scopes* y el mensaje ya formateado) y las métricas de `Microsoft.Extensions.AI`;
  - sin endpoint no se registra ningún exportador; un endpoint inválido termina con el código 2;
  - al arrancar con telemetría, una línea de registro dice hacia dónde se envía;
  - nombre de servicio fijo `sandbox-runner`;
  - el proceso *one-shot* vacía la telemetría pendiente al terminar.
- **Panel en compose**:
  - servicio `dashboard` en el perfil opcional `dashboard`, con Aspire Dashboard fijado por etiqueta y digest;
  - endurecido como el `runner` (`read_only` con un `tmpfs` en `/tmp`, sin capacidades, `no-new-privileges`, límites de memoria y CPU);
  - UI y OTLP publicados solo en `127.0.0.1`;
  - el `runner` recibe `OTEL_EXPORTER_OTLP_ENDPOINT` interpolada y vacía por defecto.
- **Dependabot**: ecosistema `docker-compose`, para que la imagen del panel también reciba actualizaciones.
- **Tests**:
  - spans y contenido con un `ActivityListener` en memoria, sin exportador ni red;
  - que ninguna traza contiene el texto de error del proveedor;
  - activación y validación del endpoint en el Runner;
  - tests de aprendizaje que fijan el comportamiento observado de la librería.
- **Documentación**:
  - documento nuevo `docs/learn/13-observabilidad-de-agentes.md`;
  - actualizados 07 (perfil del panel), 05 (auditoría y trazas), 04 (`UseOpenTelemetry`) y 09 (Dependabot);
  - glosario, índice y *backlog*, `docs/plan.md` y `README.md`;
  - ADR 0006 sobre la telemetría desactivada y sin contenido por defecto.

### Fuera de alcance

- **Métricas propias** (contadores de tools, paradas por guardrail): en un proceso *one-shot* aportan poco frente a las trazas. Se exportan solo las de `Microsoft.Extensions.AI`.
- **Instrumentación HTTP** del cliente hacia DeepSeek: añade poco y es otra vía por la que podría colarse información.
- **Panel en producción**, almacenamiento duradero de la telemetría, muestreo y autenticación del OTLP entrante: Aspire Dashboard es una herramienta de desarrollo.
- **Escaneo del panel con Grype en la CI**: no es una imagen que construya el proyecto, y solo se usa en desarrollo.
- **Cambios en la CI**: el check `image` ejecuta el Runner sin endpoint, así que no cambia.
- **Sustituir los registros actuales**: el registro de uso y la auditoría siguen en la consola, para cuando no hay panel.

## Capabilities

### New Capabilities

- `agent-observability`:
  - qué telemetría emite el sistema (spans de la ejecución, del modelo, de las tools y de las confirmaciones; logs; métricas de la librería);
  - cuándo se activa la exportación y cómo se valida el endpoint;
  - la captura de contenido opcional y su aviso;
  - el nombre del servicio y el vaciado al terminar;
  - que la telemetría no lleva contenido por defecto.

### Modified Capabilities

- `chat-client`, "Errores del proveedor sin filtrar la clave": el texto de error del proveedor tampoco puede llegar a la telemetría.
- `container-build`: requisitos nuevos para el panel de trazas opcional en compose (perfil, imagen fijada, endurecimiento y puertos solo locales) y para el endpoint de telemetría del `runner`, vacío por defecto.
- `dependency-updates`, "Actualizaciones de versión periódicas": incluye las imágenes de compose, con un cuarto ecosistema.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: la fuente de trazas del agente y las opciones de telemetría;
  - en `src/Sandbox.Runner/`: el registro de OpenTelemetry y su validación;
  - en los tests: utilidades para capturar spans, tests del agente, de la factoría del cliente, del Runner y de aprendizaje.
- **Ficheros modificados**:
  - `DeepSeekChatClientFactory` (cliente de OpenTelemetry en el pipeline), `SalesAgent` (span raíz y de confirmación) y `RunnerHost`;
  - `compose.yaml`, `.github/dependabot.yml` y `Directory.Packages.props`;
  - `docs/learn/` (13 nuevo, 07, 05, 04, 09, glosario e índice), `docs/adr/0006-…`, `docs/plan.md` y `README.md`.
- **Dependencias nuevas**, solo en `Sandbox.Runner`: `OpenTelemetry.Extensions.Hosting` y `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 (Apache-2.0; última estable en NuGet a 2026-11-06). Imagen nueva en compose: `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.4` (última estable en MCR a 2026-11-07; el explore probó la 13.6.2).
- **Comportamiento visible**: sin endpoint, nada cambia. Con endpoint, una línea de registro más al arrancar y unos cientos de milisegundos para vaciar la telemetría al terminar.
- **Conceptos nuevos y dónde se documentan**:
  - documento 13: trazas, spans y contexto; logs correlacionados; métricas; OTLP y exportadores; convenciones semánticas GenAI (experimentales); datos sensibles en la telemetría; procesos *one-shot* y vaciado; el coste de un endpoint sin receptor;
  - documento 07: el panel como servicio opcional endurecido;
  - glosario: span, traza, recurso, convenciones semánticas, exportador, *flush*.
