# Design

## Context

Motivación y alcance en `proposal.md`. Estado de partida relevante:

- `DeepSeekChatClientFactory` construye el `IChatClient` compartido con `ChatClientBuilder`: `ProviderErrorTranslatingChatClient` y `UsageLoggingChatClient` sobre el cliente del SDK de OpenAI. En `ChatClientBuilder`, el primer `Use` queda por fuera.
- `SalesAgent.RunAsync` abre el *scope* de auditoría con un `RunId` nuevo, envuelve el cliente compartido en `RunBudgetChatClient` (que delega `GetService`) y crea un `FunctionInvokingChatClient` con un `FunctionInvoker` propio. Llama al bucle una o varias veces: tantas como pausas de confirmación haya (D2 de `add-tool-approval`).
- `RunnerHost.CreateBuilder` añade a la configuración las variables de entorno, la línea de comandos y los secretos por fichero. Registra la consola con *scopes* por la salida de errores y valida las opciones al arrancar; una opción inválida da el código 2.
- `RunnerHost.RunAsync` crea el host con `using`: se libera en todas las salidas posteriores a `Build`.
- Los tests del agente usan `AgentHarness` con un `ScriptedChatClient` directo, sin el pipeline de la factoría. Los del Runner sustituyen el `IChatClient` registrado.
- `compose.yaml` tiene un único servicio `runner`, endurecido, con la API key como secreto por fichero.

### Explore: experimentos y resultados

Dos spikes en el scratchpad (`spike-fase5c`), fuera del repositorio, el 2026-11-06. Versiones: `Microsoft.Extensions.AI` 10.11.0, `OpenTelemetry.Extensions.Hosting` y `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1, y la imagen `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2`. Los programas imitan la forma de `SalesAgent`: un modelo falso con guion, un cliente intermedio que delega `GetService` como `RunBudgetChatClient`, un `FunctionInvoker` propio, `ApprovalRequiredAIFunction` con pausa y reanudación, y un exportador de consola propio para ver lo que se exportaría.

Fuentes oficiales consultadas el mismo día: la referencia de `OpenTelemetryChatClient`, que dice implementar las convenciones semánticas GenAI v1.41, experimentales; la guía de Microsoft Learn del Aspire Dashboard autónomo, que habla de herramienta de desarrollo, token de acceso en la UI y puertos 18888 y 18889; y la lista de ecosistemas de Dependabot en GitHub Docs.

**Spike sin Docker** (exportador propio en memoria y receptor OTLP/HTTP mínimo en Node):

| Exp. | Qué se probó | Resultado observado |
|---|---|---|
| A1 | Spans de tools con `FunctionInvoker` propio y un cliente intermedio | `FunctionInvokingChatClient` emite un `execute_tool <nombre>` por invocación, con `gen_ai.tool.name`, `gen_ai.tool.call.id` y `gen_ai.tool.description`. Dentro del `FunctionInvoker`, `Activity.Current` es ese span: un log emitido allí lleva su `SpanId` |
| A2a | Sin span raíz propio | Cada `GetResponseAsync` abre un `orchestrate_tools` **raíz**: una ejecución con confirmación sale en **dos trazas**, con el span de aprobación suelto |
| A2b | Con span raíz propio (`invoke_agent`) | **No** aparece `orchestrate_tools`: los `chat` y los `execute_tool` cuelgan del raíz, en una sola traza. Los `execute_tool` salen con la fuente del span raíz, no con la del cliente de chat |
| A3 | El proveedor lanza una excepción cuyo texto repite parte de la clave | El span `chat` queda con `status=Error`, `error.type` igual al tipo de la excepción y **el mensaje crudo en `StatusDescription`** |
| A4 | `EnableSensitiveData = true` | Los `chat` llevan `gen_ai.input.messages` y `gen_ai.output.messages` (con los argumentos y los resultados de las tools dentro de los mensajes) y las definiciones completas de las tools. Sin span raíz, `execute_tool` lleva además `gen_ai.tool.call.arguments`. **Con span raíz no lo lleva**, tampoco usando la misma fuente para todo; la causa no está verificada |
| A5 | `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` sin `EnableSensitiveData` | Sin efecto: no aparece contenido, y la cadena no existe en el ensamblado |
| A6 | Valores por defecto | Solo `gen_ai.tool.definitions` con los **nombres** de las tools, el modelo, el servidor (`api.deepseek.com:443`) y los tokens. Sin mensajes ni argumentos |
| A7 | Logs con `IncludeScopes` | Llevan `TraceId` y `SpanId` del span activo; las propiedades de los *scopes* (`RunId`, `CustomerId`) viajan como atributos |
| A8 | Proceso *one-shot* con receptor | Al parar y liberar el host se envían `/v1/traces`, `/v1/metrics` y `/v1/logs`. El cierre tarda 255 ms |
| A9 | Endpoint configurado sin receptor (local, Windows) | Unos **8,7 s más** al salir, con gRPC y con HTTP, sin ningún mensaje |
| A10 | Cuerpo OTLP del log sin y con `IncludeFormattedMessage` | Sin la opción, el cuerpo es la **plantilla** (`Tool {Tool}: resultado {Outcome}`). Con ella, `Tool get_order: resultado ok`; la plantilla queda como atributo |

**Spike con Docker** (el panel en compose y el programa del spike en un contenedor de runtime en la misma red):

| Exp. | Qué se probó | Resultado observado |
|---|---|---|
| D1 | Imagen del panel | Unos 239 MB y usuario `1654` (no root). Variables: UI 18888, OTLP gRPC 18889, OTLP HTTP 18890 y MCP 18891 (este no aparece escuchando en el arranque). Digest `sha256:e4220c55fa3c3e40ad2d733ef07973392299b420e9bde6924e8f6a1e71ff01a8` |
| D2 | `read_only: true`, `cap_drop: ALL`, `no-new-privileges` | **No arranca**: `IOException: Read-only file system` en `Directory.CreateTempSubdirectory`. Con `tmpfs: [/tmp:size=32m]` arranca; solo avisa de que las claves de Data Protection son efímeras |
| D3 | Memoria | Unos 59 MiB en reposo y unos 60 MiB tras recibir dos ejecuciones |
| D4 | Seguridad por defecto | La UI pide un token efímero, distinto en cada arranque. El OTLP no tiene autenticación y el panel lo avisa: "OTLP server is unsecured" |
| D5a | `runner` con endpoint `http://dashboard:18889` y el perfil apagado | Unos **11,9 s más** al salir (el nombre no resuelve), con gRPC y con HTTP |
| D5b | Con el panel en marcha | Envío aceptado con un cierre de 230 a 530 ms. En la UI, el usuario vio los registros de las dos ejecuciones enlazados con su traza. La vista de trazas no se revisó a ojo |
| D6 | Recurso en el panel | Cada proceso aparece como un recurso distinto (`spike-runner-fbc9e104`, `spike-runner-4cd995a6`), por el identificador de instancia que se genera por defecto |

## Goals / Non-Goals

**Goals:**
- Que una ejecución del agente se lea en el panel como una sola traza, incluidas las pausas de confirmación.
- Que la telemetría no abra una vía nueva de fuga: nada de contenido por defecto y nunca el texto de error del proveedor.
- Que sin endpoint todo siga exactamente igual, también en la CI y en los tests.
- Que todo lo que se especifica se pruebe sin red, con un oyente de spans en memoria.

**Non-Goals:**
- Telemetría de los servicios de aplicación o del dominio: no conocen OpenTelemetry, y la auditoría ya les da voz por `ILogger`.
- Propagar el contexto de traza a DeepSeek o a otros procesos.
- Ajustar el muestreo, los lotes o los tiempos de espera del exportador.

## Decisions

### D1. Una sola fuente, `Sandbox.Agents`, para trazas y métricas

El span raíz, el de confirmación y el cliente de OpenTelemetry del modelo usan la misma fuente (`sourceName` de `UseOpenTelemetry`), expuesta como constante en `Sandbox.Agents`. El Runner la registra en el exportador con `AddSource` y `AddMeter`; `OpenTelemetryChatClient` crea también su `Meter` con ese nombre.

Alternativa descartada: dos fuentes, una para el agente y otra para el modelo. Con span raíz, los `execute_tool` salen igualmente con la fuente del agente (A2b), así que la separación no aportaría un filtro limpio.

### D2. El cliente de OpenTelemetry, por fuera del traductor de errores

En la factoría, `UseOpenTelemetry` se añade **antes** que `ProviderErrorTranslatingChatClient`, así que queda por fuera. Solo ve `ChatProviderException`, cuyo mensaje se construye sin el texto del proveedor. Por A3, en la posición contraria el panel mostraría el fragmento de la clave.

- El test es el escenario "Clave rechazada con telemetría" de `chat-client`: el 401 simulado con `StubHttpMessageHandler` repite un fragmento de clave, y se comprueba que no llega al span.
- Al ir por fuera, `GetService(typeof(ActivitySource))` lo encuentra a través de `RunBudgetChatClient`, y así `FunctionInvokingChatClient` emite los `execute_tool` (A1).
- `UseOpenTelemetry` se llama **sin** `ILoggerFactory`. Así, ningún contenido puede acabar en los registros de consola, ni siquiera con la captura activada (escenario "La consola no muestra el contenido").

### D3. Span raíz y span de confirmación en `SalesAgent`

`RunAsync` abre `invoke_agent sales-agent` antes del *scope* de auditoría y lo cierra al final, así que envuelve todas las vueltas del bucle (A2b).

- **Atributos al abrir**: `gen_ai.operation.name` = `invoke_agent`, `gen_ai.agent.name` = `sales-agent` y `sandbox.run.id` (el mismo `RunId` del *scope*).
- **Atributos al cerrar**: `sandbox.stop_reason`, `sandbox.model_calls`, `gen_ai.usage.input_tokens` y `gen_ai.usage.output_tokens`.
- **Parada por un guardrail**: estado `Error` con el nombre del motivo como descripción.
- **Excepción**: si sale una excepción del agente (por ejemplo `ChatProviderException`), estado `Error` con el **nombre del tipo** como descripción. No se usa `AddException`, que copiaría el mensaje y la traza a un evento.

Cada decisión humana se envuelve en `approval <tool>`, con `gen_ai.tool.name` y `sandbox.approval.decision` (`Approved` o `Denied`). Dura lo que tarda el aprobador y nunca lleva los argumentos.

Los atributos con prefijo `gen_ai.` siguen las convenciones semánticas. Los que no tienen equivalente usan el prefijo propio `sandbox.`, para no inventar nombres dentro de un espacio ajeno.

### D4. Resultado de cada tool en su span

El `FunctionInvoker` añade `sandbox.tool.outcome` a `Activity.Current`, que es el `execute_tool` en curso (A1). El valor es `OK` (`ToolInvocation.Succeeded`) o el código de error, el mismo que ya va a `ToolInvoked`. Las tools bloqueadas por la allow-list también pasan por el `FunctionInvoker`, así que su span lleva `BLOCKED_BY_POLICY`. Una denegación humana no pasa por él (A3 de `add-tool-approval`): queda en el span `approval`.

### D5. Captura de contenido: `Telemetry:CaptureContent`

Es una opción propia, porque la variable estándar no tiene efecto (A5). Vive en `TelemetryOptions` (sección `Telemetry`) en `Sandbox.Agents`: la factoría la necesita para fijar `EnableSensitiveData`. Por defecto vale `false`.

- **Activada y con exportación**: el Runner registra un aviso al arrancar.
- **Activada y sin exportación**: los atributos se generan, pero sin ningún oyente nadie los recoge. No se avisa.

Rareza observada (A4): con span raíz, `execute_tool` no lleva `gen_ai.tool.call.arguments`. No se intenta corregir: los argumentos y los resultados ya viajan en los mensajes de los `chat`, y un test de aprendizaje fija el comportamiento para detectar si la librería cambia.

### D6. Activación y validación en el Runner

- **Activación**: el Runner lee `OTEL_EXPORTER_OTLP_ENDPOINT` de su configuración, que incluye las variables de entorno con su nombre tal cual, la línea de comandos y los secretos. Si está vacía o no existe, no registra nada de OpenTelemetry: ni proveedores ni exportador.
- **Validación**: si tiene valor, se valida al arrancar con un validador de opciones, como el resto. Debe ser una URL absoluta `http` o `https`; si no lo es, `OptionsValidationException` y código 2.
- **Registro**: si es válida, se registran `AddOpenTelemetry()` con el recurso `sandbox-runner`, trazas (`AddSource`), métricas (`AddMeter`), logs (`IncludeScopes` e `IncludeFormattedMessage`, por A7 y A10) y `UseOtlpExporter()` sin argumentos. Así, el exportador toma de la configuración el endpoint y, si se definen, el protocolo y las cabeceras estándar.
- **Aviso**: tras arrancar, el Runner registra `Telemetría OTLP activa hacia {Endpoint}` con solo esquema, host y puerto. Una URL con credenciales embebidas no las dejaría en el registro.

Alternativa descartada: registrar siempre el SDK sin exportador. Cuesta poco, pero "no hay nada registrado" es la garantía más simple de que sin endpoint no cambia nada, y se comprueba pidiendo el `TracerProvider` al contenedor.

Por verificar en la implementación: que `UseOtlpExporter()` toma el endpoint de la configuración del host y no solo del entorno. Si no fuera así, se le pasa el endpoint validado de forma explícita, sin cambiar el contrato.

### D7. Vaciado al terminar: lo hace la liberación del host

Los proveedores de OpenTelemetry se liberan con el contenedor de servicios, y al liberarse envían lo pendiente (A8). `RunnerHost.RunAsync` ya libera el host en todas las salidas. No hace falta ningún `ForceFlush` explícito.

### D8. Recurso con nombre fijo y sin identificador de instancia

El servicio se llama `sandbox-runner` y no genera identificador de instancia (`autoGenerateServiceInstanceId: false`). Así, el panel no acumula un recurso por cada proceso (D6). Cada ejecución ya se distingue por su traza y su `RunId`.

Por verificar en la implementación: que el panel agrupa entonces las ejecuciones en un solo recurso. Si no lo hiciera, se acepta el comportamiento por defecto y se documenta.

### D9. Compose: perfil `dashboard`

```yaml
dashboard:
  image: mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2@sha256:e4220c55…
  profiles: [dashboard]
  read_only: true
  tmpfs: [/tmp:size=32m]
  cap_drop: [ALL]
  security_opt: [no-new-privileges:true]
  mem_limit: 256m
  cpus: 1.0
  ports:
    - "127.0.0.1:18888:18888"   # UI
    - "127.0.0.1:4317:18889"    # OTLP gRPC, para dotnet run en local
runner:
  environment:
    OTEL_EXPORTER_OTLP_ENDPOINT: ${OTEL_EXPORTER_OTLP_ENDPOINT:-}
```

- **Uso**: `docker compose --profile dashboard up -d dashboard`. Después, `OTEL_EXPORTER_OTLP_ENDPOINT=http://dashboard:18889 docker compose run --rm runner …` en contenedor, o `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run …` en local. La URL de acceso con el token está en `docker compose logs dashboard`.
- **Sin `depends_on`**: el `runner` no debe arrancar el panel por su cuenta.
- **Puertos que no se publican**: el OTLP HTTP y el MCP.
- **El token de la UI se mantiene**. El OTLP sin autenticación se acepta: solo se publica en `127.0.0.1` y, dentro de la red de compose, solo están el `runner` y el panel (D4).
- **`mem_limit: 256m`**: deja margen sobre los unos 60 MiB observados (D3).

Alternativa descartada por el usuario en el explore: un fichero override (`compose.dashboard.yaml`).

### D10. Dependabot: ecosistema `docker-compose`

Se añade una entrada `docker-compose` en `/`, semanal y agrupada, como el resto. Según GitHub Docs, el ecosistema admite actualizaciones de versión pero **no** de seguridad, y "funciona de forma similar a Docker". La documentación no aclara qué nombres de fichero lee ni cómo trata etiqueta con digest. Se comprueba con el primer pull request real y se anota en el documento 09, como el pendiente que ya existe para Docker.

### D11. Tests sin exportador

Un oyente de spans de prueba (`ActivityListener`) sobre la fuente `Sandbox.Agents` muestrea todo y guarda los spans terminados. Como los tests corren en paralelo y el oyente es global, cada test abre su propio span padre y filtra por su `TraceId`.

- **Agente**: `AgentHarness` admite envolver el modelo con `UseOpenTelemetry` sobre la misma fuente, con y sin captura de contenido. Así se prueban los requisitos de trazas, contenido y confirmaciones.
- **Factoría**: el 401 con fragmento de clave (D2) y la captura de contenido.
- **Runner**: el endpoint inválido da el código 2; el `TracerProvider` está registrado o no según el endpoint; la línea de aviso aparece o no; y el aviso de contenido.
- **Aprendizaje** (fijan A2a, A2b, A3 y A4): sin raíz hay `orchestrate_tools` y dos trazas; con raíz no; el estado del span lleva el mensaje de la excepción; y con raíz `execute_tool` no lleva argumentos.

El vaciado real (A8) y la vista en el panel se comprueban a mano con compose, como en el explore.

### D12. ADR 0006

La política "telemetría desactivada por defecto, sin contenido salvo opción explícita y nunca con el texto de error del proveedor" trasciende este change: cualquier telemetría futura debe respetarla. Se registra como `docs/adr/0006-telemetria-opcional-y-sin-contenido.md`.

### Separación entre dominio e invocación del LLM

El dominio y la aplicación no cambian ni conocen OpenTelemetry. `Sandbox.Agents` solo usa `System.Diagnostics` (de .NET) y `UseOpenTelemetry` de `Microsoft.Extensions.AI`, que ya tenía. Los paquetes de OpenTelemetry quedan en el Runner, la raíz de composición. Las reglas de capas de `solution-layout` no cambian.

## Risks / Trade-offs

- [Un endpoint puesto sin receptor alarga la salida entre 9 y 12 s, sin avisar] → Endpoint vacío por defecto, en local y en compose, y línea de aviso al arrancar con el destino. Se documenta en el 13 y en el 07.
- [Las convenciones GenAI son experimentales y sus nombres pueden cambiar entre versiones] → Los tests solo fijan los atributos propios y los imprescindibles (`gen_ai.operation.name`, tokens). Los de aprendizaje avisan de cambios de la librería.
- [Con la captura activa, el panel guarda en memoria las peticiones y los resultados] → Desactivada por defecto, aviso al arrancar, datos sintéticos y panel solo en `127.0.0.1`. Queda recogido en el ADR 0006.
- [El OTLP del panel no tiene autenticación] → Solo en `127.0.0.1` y en la red de compose. Es una herramienta de desarrollo; nunca se expone fuera.
- [Paquetes nuevos en la imagen final que Grype puede marcar] → Política del ADR 0002. Si el check `image` falla por una vulnerabilidad, se trata en el change.
- [Dependabot podría no reconocer `compose.yaml` o reescribir mal el digest] → Se comprueba con el primer pull request (D10). Mientras tanto, la imagen del panel se puede actualizar a mano.
- [`UseOtlpExporter()` podría no leer la configuración del host] → Plan B en D6, sin cambiar el contrato.

## Migration Plan

No hay datos ni contratos que migrar. Sin endpoint, el comportamiento es el actual. Para deshacer el change basta con revertirlo. Los escenarios y los códigos de salida no cambian.

## Open Questions

- ¿Agrupa el panel las ejecuciones en un solo recurso sin identificador de instancia (D8)? No cambia ni las specs ni las tareas: se acepta lo que haga y se documenta.
