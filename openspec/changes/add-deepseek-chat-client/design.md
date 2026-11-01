# Design

## Context

Motivación y alcance en `proposal.md`; requisitos en `specs/chat-client`, `specs/runner-host` y los deltas de `container-build` y `continuous-integration`.

Estado de partida observado el 2026-11-01:

- **Código**:
  - `src/Sandbox.Runner/Program.cs` es `return 0;`;
  - `Sandbox.Agents`, `Sandbox.Application` y `Sandbox.Domain` están vacíos;
  - el único paquete es `xunit.v3` 4.0.2.
- **Tests**:
  - `Sandbox.Scenarios.Tests` referencia al Runner y contiene `LayerRulesTests`, que prohíbe librerías de IA (`Microsoft.Extensions.AI`, `OpenAI`…) en el dominio y la aplicación, y `GlobalizationTests`;
  - `Sandbox.Agents.Tests` solo tiene un test de cableado temporal.
- **Contenedor y CI**:
  - el servicio `runner` de `compose.yaml` usa `network_mode: none`, no declara secretos y su sistema de ficheros raíz es de solo lectura;
  - el job `image` de la CI ejecuta la imagen final sin red ni clave y espera el código 0.
- **Documentación**:
  - el `README.md` muestra `deepseek-chat` como modelo y `dotnet user-secrets set` sin mencionar el entorno;
  - el documento 04 marca "(A verificar en la fase 2)" cómo fijar el endpoint.
- **La clave del responsable** está en `%USERPROFILE%\.secrets\deepseek_apikey.txt`, fuera del repositorio.

### Explore previo: experimentos y resultados

Los experimentos se hicieron el 2026-11-01 con una consola desechable en el scratchpad de la sesión, fuera del repositorio. Usaron `OpenAI` 2.14.0, `Microsoft.Extensions.AI` y `Microsoft.Extensions.AI.OpenAI` 10.11.0, y la clave del responsable, que se leyó de su fichero sin mostrarla. Las peticiones y las respuestas se registraron sin cabeceras. En total se gastaron unos 2.600 tokens.

**Consultas a fuentes primarias (2026-11-01):**

| Qué | Resultado | Fuente |
|---|---|---|
| Paquetes | `Microsoft.Extensions.AI`, `.Abstractions` y `.OpenAI` 10.11.0; `OpenAI` 2.14.0; `Microsoft.Extensions.Hosting` y `.Configuration.KeyPerFile` 10.0.14. Existen versiones 11.0.0-rc, descartadas por ser preliminares | API de NuGet (`v3-flatcontainer`) |
| Modelos de DeepSeek | `deepseek-flash` (DeepSeek-V4.1-Flash-1017) y `deepseek-v4-pro` (V4-Pro-0813), los dos con 1M de contexto, *tool calls*, salida JSON y *thinking* activado por defecto | `api-docs.deepseek.com`, portada y *Models & Pricing* |
| Precio de `deepseek-flash` | 0,30 $/M de entrada (sin caché) y 1,20 $/M de salida en hora punta; la mitad fuera de ella | *Models & Pricing* |
| *Key-per-file* | Cada fichero de un directorio es una clave y `__` actúa como separador. La ruta debe ser absoluta y puede ser opcional. "Se usa en escenarios de Docker" | Microsoft Learn, *Configuration providers* |
| User Secrets | `CreateApplicationBuilder` solo las carga en el entorno `Development` | Microsoft Learn, *Configuration providers* |

**Experimentos contra la API:**

| Experimento | Observado |
|---|---|
| Endpoint | `OpenAIClientOptions.Endpoint = https://api.deepseek.com`; el SDK llama a `POST /chat/completions` |
| Clave inválida | HTTP 401, `ClientResultException` con `Status=401`. El mensaje incluye `Your api key: ****pike is invalid`, es decir, **los 4 últimos caracteres de la clave**, y un `request_id` |
| "hola" con *thinking* por defecto | 1,3 s y 53 tokens, de ellos 11 de razonamiento. La respuesta trae `reasoning_content`, que el adaptador convierte en `TextReasoningContent`, separado del `TextContent` |
| "hola" con *thinking* desactivado | Con `ChatCompletionOptions.Patch.Set("$.thinking"u8, {"type":"disabled"})` dentro de `ChatOptions.RawRepresentationFactory`, la petición lleva `"thinking":{"type":"disabled"}`. 1,3 s, 15 tokens y sin razonamiento. `Patch` es `JsonPatch` de `System.ClientModel` 1.15, **experimental (`SCME0001`)** |
| *Tool call* con `UseFunctionInvocation` | Funciona con *thinking* activado y desactivado: dos peticiones, 2,7–2,9 s y unos 750–800 tokens. La herramienta añade unos 300 tokens al *prompt*. Con *thinking* activado y herramientas, `reasoning_content` llegó vacío |
| Nombre de la tool | `AIFunctionFactory.Create` sobre una función local generó `_Main_g_GetOrderStatus_0_2`. Hay que dar siempre un nombre explícito (dato para la fase 4) |
| Caché de *prompt* | Automática: en la segunda petición de una *tool call*, 256 de 399 tokens salieron de caché |
| Nombres antiguos | `deepseek-chat` y `deepseek-reasoner` responden 200, pero el campo `model` de la respuesta dice `deepseek-flash`: **remapeo silencioso** |
| `deepseek-v4-pro` | Responde con el mismo "hola" en 2,0 s |

**Suposiciones sin verificar** (se comprueban en las tareas):

- Que `docker compose` monte un secreto de tipo fichero en `/run/secrets/<target>` con el nombre de `target` (`DeepSeek__ApiKey`) y que el usuario 1654 de la imagen *chiseled* pueda leerlo.
- Que *key-per-file* recorte o no el salto de línea final del fichero.
- Que el cliente HTTP de .NET no necesite escribir en disco con el sistema de ficheros raíz de solo lectura (es decir, que no haga falta `tmpfs`).
- Que el *binder* de configuración rellene propiedades `init`.
- Que todas las dependencias nuevas sean MIT.
- Qué pasa con *thinking* activado cuando el razonamiento no viene vacío en un turno con herramientas (¿hay que reenviarlo?). Es de la fase 4; aquí no hace falta.

### Comprobado durante el apply

| Qué | Observado (2026-11-01) | Consecuencia |
|---|---|---|
| Licencias | Las cinco dependencias declaran MIT en su `.nuspec` | Se añaden sin consulta |
| Dependencias transitivas | `Microsoft.Extensions.AI.OpenAI` trae `OpenAI` 2.14.0, y este `System.ClientModel` 1.15.0; `Microsoft.Extensions.Options` llega a `Sandbox.Agents` como transitiva | Solo se referencian `Microsoft.Extensions.AI` y `.AI.OpenAI` en `Sandbox.Agents`, y `Hosting` y `KeyPerFile` en el Runner |
| Salto de línea en *key-per-file* | Con un experimento en el scratchpad (`KeyPerFile` 10.0.14 en Windows): `abc\r\n` se lee como `abc`, pero `abc\n` se lee como `abc\n` y `abc\n\n` como `abc\n\n`. Solo quita **un** `Environment.NewLine` final, que en Windows es `\r\n` y en Linux `\n` | **El recorte lo hace el código**: `PostConfigure` de `DeepSeekOptions` aplica `Trim()` a la clave. Lo prueban los tests de `RunnerConfigurationTests` con `\n`, `\r\n` y espacios |
| Propiedades `init` y *binder* | No hizo falta comprobarlo: `DeepSeekOptions` usa `set`, porque `PostConfigure` modifica la clave. Es una clase de opciones, no un DTO | Sin cambio en las specs |
| Reintentos del SDK | Medido en el scratchpad con un transporte simulado: por defecto, `OpenAI` 2.14.0 hace **4 llamadas** (1 + 3 reintentos) ante un 429 o un fallo de conexión, y en el fallo de conexión termina con un `AggregateException`. Con `ClientRetryPolicy(maxRetries: 0)` hace 1 llamada; el 429 llega como `ClientResultException` con `Status=429`, y el fallo de conexión como `ClientResultException` con `Status=0` y un `HttpRequestException` dentro | Se fija `maxRetries: 0`: cada reintento cuesta tokens y no queda registrado, y el `AggregateException` escaparía a la traducción de errores (código 1 en vez de 3). El traductor trata `Status=0` como `Network`. La política de reintentos se decide en la fase 5, como dice la propuesta |
| Raíz de contenido | `Host.CreateApplicationBuilder` usa el directorio actual como raíz de contenido (Microsoft Learn). Con `dotnet run --project <ruta>`, el directorio actual es el de la terminal, no el del proyecto (comprobado en el scratchpad con un proyecto sin perfil de lanzamiento), así que desde la raíz del repositorio no se encontraría `appsettings.json` | `ContentRootPath = AppContext.BaseDirectory`, que en el contenedor es `/app` como antes |
| `ValidateOnStart` | Con un único tipo de opciones, `StartAsync` lanza `OptionsValidationException` (no `AggregateException`) | El Runner la traduce al código 2 |
| Cancelación | Con el transporte simulado, una cancelación durante la petición llega como `OperationCanceledException`, sin envolver en `ClientResultException` | El traductor de errores no la toca |
| Secreto de compose (Docker Desktop 29.9.2, Compose v5.6.1) | `docker inspect` muestra un *bind mount* de solo lectura en `/run/secrets/DeepSeek__ApiKey`. El usuario 1654 lo lee (el saludo sale con 0 y el contenedor no tiene otra fuente de la clave). Con el fichero ausente, compose solo avisa ("secret file … does not exist") y el Runner sale con 2 | No hizo falta ajustar `uid` ni `mode` del secreto |
| Sistema de ficheros de solo lectura | El saludo real funciona con `read_only: true`: el cliente HTTP no necesitó escribir en disco | No hace falta `tmpfs` |
| Registro de arranque del host | Sin clave, el host registraba "Hosting failed to start" con la traza completa de `OptionsValidationException`, además del mensaje del Runner | Se silencia la categoría `Microsoft.Extensions.Hosting.Internal.Host`: `RunnerHost.RunAsync` ya informa de todos los fallos de arranque y registra los inesperados con su detalle |

## Goals / Non-Goals

**Goals:**

- Un único punto donde se construye el `IChatClient`, en `Sandbox.Agents`, y un único punto donde se compone la aplicación, en `Sandbox.Runner`.
- Que todo el camino del cliente real (petición HTTP, JSON, traducción de errores) se pueda probar **sin red**, con un transporte HTTP simulado.
- Que la clave no pueda aparecer en la salida, en los registros, en la imagen ni en la configuración del contenedor.

**Non-Goals:**

- Abstraer varios proveedores. Solo hay DeepSeek, y `IChatClient` ya es la abstracción.
- Reintentos, *timeouts* afinados o *circuit breaker*: fase 5.

## Decisions

### D1. Reparto entre capas

| Pieza | Proyecto | Por qué |
|---|---|---|
| `DeepSeekOptions` y su validador | `Sandbox.Agents` | Son de la invocación del LLM (`AGENTS.md`, principio 1) |
| Construcción del `IChatClient` y sus decoradores | `Sandbox.Agents` | Mismo motivo; es lo único que conoce `OpenAI` |
| `ChatProviderException` (error neutral del proveedor) | `Sandbox.Agents` | El Runner y los tests no dependen de tipos de `OpenAI` |
| Host, fuentes de configuración, saludo y códigos de salida | `Sandbox.Runner` | Es la raíz de composición |
| Cliente falso | Tests | No es código de producción |

El dominio y la aplicación no cambian, y `LayerRulesTests` lo sigue comprobando.

### D2. Opciones y validación

- `DeepSeekOptions`, sección `DeepSeek`, con cuatro propiedades:
  - `Endpoint`, por defecto `https://api.deepseek.com`;
  - `Model`, por defecto `deepseek-flash`;
  - `Thinking`, por defecto `false`;
  - `ApiKey`, sin valor por defecto.
- **Validación propia** (`IValidateOptions<DeepSeekOptions>`) en vez de *data annotations*. Así no hace falta otro paquete, los mensajes se controlan y **nunca incluyen el valor** de la opción (la clave). Se ejecuta al arrancar (`ValidateOnStart`).
- Los valores por defecto viven en el código y en `appsettings.json`, que no lleva clave. La clave puede tener espacios o un salto de línea final si viene de un fichero, así que se recorta antes de usarla. La tarea correspondiente comprueba qué hace *key-per-file*.

Alternativa: `[Required]` y `ValidateDataAnnotations()`. Es más corta, pero sus mensajes genéricos podrían cambiar y obliga a añadir `Microsoft.Extensions.Options.DataAnnotations`.

### D3. Construcción del cliente

```
IChatClient
  = ProviderErrorTranslatingChatClient     (ClientResultException/HttpRequestException -> ChatProviderException)
    -> UsageLoggingChatClient               (modelo pedido, modelo que respondió, duración, tokens)
      -> ConfigureOptions                   (ModelId y, si Thinking=false, el JsonPatch)
        -> OpenAI ChatClient.AsIChatClient()  (Endpoint, ApiKeyCredential)
```

- **`OpenAIClient` con `ApiKeyCredential` y `OpenAIClientOptions.Endpoint`**, tal como se comprobó en el explore.
- **El razonamiento** se desactiva en `ChatOptions.RawRepresentationFactory`, devolviendo un `ChatCompletionOptions` con `Patch.Set("$.thinking"u8, …)`. Es el **único** fichero con `#pragma warning disable SCME0001`, con un comentario que lo justifica: la API es experimental, y si cambia solo falla aquí. No se usa `<NoWarn>` global.
- **No se usa `UseLogging()` de `Microsoft.Extensions.AI`**, porque puede registrar los mensajes completos con niveles detallados **(no verificado)**. Un `DelegatingChatClient` propio registra solo lo que pide la spec y avisa si el modelo que respondió no coincide con el pedido (el remapeo del explore).
- **Traducción de errores**: `ProviderErrorTranslatingChatClient` captura `ClientResultException` y crea un `ChatProviderException` con el código HTTP y una categoría estable (`Authentication`, `RateLimited`, `ProviderError`). Captura también `HttpRequestException` (`Network`). **Nunca copia el mensaje del proveedor.** `OperationCanceledException` se propaga sin tocar.

Alternativa: dejar que el Runner capture `ClientResultException`. Lo ataría a `OpenAI`, y el cliente falso no podría reproducir el mismo error sin depender de ese paquete.

### D4. Host y fuentes de configuración

- `Host.CreateApplicationBuilder(args)` ya trae `appsettings.json`, `appsettings.{Environment}.json`, User Secrets (en `Development`, si el proyecto tiene `UserSecretsId`), variables de entorno y línea de comandos. Después se añade `AddKeyPerFile("/run/secrets", optional: true)`, que al ir al final tiene la máxima prioridad.
- **El directorio de secretos es un parámetro** del método que crea el *builder*. Por defecto es `/run/secrets`, y los tests pasan un directorio temporal. No se configura desde la propia configuración, para no crear una dependencia circular.
- **`Properties/launchSettings.json`** fija `DOTNET_ENVIRONMENT=Development` para `dotnet run` en local, porque las User Secrets solo se cargan en ese entorno. El `UserSecretsId` va en el `.csproj`: es un identificador, no un secreto.
- **Salida y registros separados**: el resultado del saludo va a la salida estándar, y los registros a la de errores (`LogToStandardErrorThreshold`). Así un script puede leer la respuesta sin mezclarla con registros.

### D5. Flujo del Runner y códigos de salida

```
build host --> StartAsync (ValidateOnStart)
   |  OptionsValidationException ------------------> 2 "Configuración inválida: falta DeepSeek:ApiKey"
   v
GreetingRunner.RunAsync --> IChatClient.GetResponseAsync("<saludo fijo>")
   |  ChatProviderException ----------------------> 3 "El proveedor rechazó la autenticación (HTTP 401)"
   |  otra excepción ------------------------------> 1 "Error inesperado" (detalle solo en el registro)
   v
imprime texto, modelo y tokens --------------------> 0
```

- Los códigos están en una clase estática (`ExitCodes`) y forman parte de la spec `runner-host`.
- El saludo es un texto fijo en español que pide una respuesta muy corta, para minimizar los tokens.
- Los tests de `Sandbox.Scenarios.Tests` ejecutan este flujo completo con el cliente falso, sustituyendo el registro del `IChatClient`.

### D6. Pruebas sin red del cliente real

Además del cliente falso, `Sandbox.Agents.Tests` prueba **el cliente real con un transporte HTTP simulado**: un `HttpMessageHandler` que devuelve respuestas JSON grabadas del explore, pasado al SDK con `HttpClientPipelineTransport`. Así se comprueba sin red que:

- la petición lleva `model: deepseek-flash` y `thinking: {type: disabled}`, y no lo lleva cuando `Thinking=true`;
- un 401 cuyo cuerpo contiene `****pike` se convierte en `ChatProviderException` sin ningún fragmento de la clave;
- el registro muestra los dos modelos y avisa del remapeo cuando el `model` de la respuesta es distinto.

Para comprobar los registros, un `ILoggerProvider` mínimo dentro de los tests, sin paquetes adicionales.

**El cliente falso** (`FakeChatClient`, que implementa `IChatClient`) vive en `tests/Sandbox.Agents.Tests/Fakes/` y se enlaza en `Sandbox.Scenarios.Tests` con `<Compile Include="…" Link="…" />`. Alternativas descartadas:
- un proyecto de utilidades de test nuevo, que cambiaría la spec `solution-layout`;
- ponerlo en `Sandbox.Agents`, que lo convertiría en código de producción.

### D7. Compose: red y secreto

```yaml
services:
  runner:
    # sin network_mode: red por defecto de compose (el control de salida es de la fase 5)
    secrets:
      - source: deepseek_api_key
        target: DeepSeek__ApiKey
secrets:
  deepseek_api_key:
    file: ${DEEPSEEK_API_KEY_FILE:-./secrets/deepseek_api_key}
```

- **La ruta del fichero se elige con `DEEPSEEK_API_KEY_FILE`.** El responsable puede apuntar a su fichero de fuera del repositorio sin copiarlo, y por defecto se usa `secrets/`, que Git ignora.
- **No hay variable de entorno con la clave**: no aparece en `docker inspect`.
- Si el fichero no existe, compose solo avisa (lo observado en la fase 1). El Runner termina con código 2, que es justo lo que pide la spec.
- Se mantienen `read_only`, `cap_drop`, `no-new-privileges` y los límites. Si el cliente HTTP necesitara escribir, se añadiría un `tmpfs` en `/tmp` justificado con el error observado.

### D8. CI: el check `image` espera el código 2

El paso de ejecución endurecida pasa a comprobar el código:

```bash
set +e; docker run … agentic-sandbox-runner:ci; code=$?; set -e
test "$code" -eq 2
```

También comprueba que la salida nombra la opción que falta. Sigue sin red, porque sin clave el Runner no llama a nadie. Así el check demuestra que la imagen arranca, carga la configuración y falla de forma controlada.

Alternativas descartadas:
- un "modo falso" activable por variable en la imagen de producción: añade una puerta trasera de pruebas;
- un secreto en la CI: no hay ninguno, y ADR 0002 no lo contempla.

### D9. Correcciones de documentación

- **`README.md`**:
  - el modelo pasa a ser `deepseek-flash`;
  - las User Secrets necesitan `Development` (lo pone `launchSettings.json` con `dotnet run`);
  - se explica cómo ejecutar en contenedor con `DEEPSEEK_API_KEY_FILE`.
- **Documento 04**: se resuelve el "(A verificar en la fase 2)" con lo observado en el explore y en el apply.

### D10. ¿ADR?

No. Las decisiones son de este change y quedan en sus specs: códigos de salida y prioridad de la configuración en `runner-host`; razonamiento por defecto en `chat-client`. Si una fase posterior cambia de proveedor o de estrategia de secretos, entonces sí.

## Risks / Trade-offs

- **[API experimental `JsonPatch`]** Puede cambiar o desaparecer en una versión de `System.ClientModel` → Está aislada en un fichero y cubierta por un test sin red que comprueba el JSON enviado. Un PR de Dependabot que la rompa saldrá en rojo en `test`.
- **[DeepSeek cambia modelos y nombres sin aviso]** → El modelo es configurable, el registro avisa del remapeo, y la documentación dice dónde comprobar los modelos vigentes.
- **[Red abierta en el `runner`]** Puede llegar a cualquier destino → Se acepta hasta la fase 5, que es la del control de salida y ya está en el backlog. El Runner solo llama a un endpoint configurado.
- **[Coste]** Cada saludo real cuesta unos 15 tokens → Despreciable. Los tests y la CI no llaman al proveedor.
- **[Permisos del secreto montado desde Windows]** El fichero podría no ser legible para el usuario 1654 → Se comprueba en las tareas. Si falla, se documenta y se ajusta `uid`/`mode` del secreto si compose lo admite **(no verificado)**.
- **[Seis paquetes nuevos]** Más superficie y más PRs de Dependabot (agrupados en uno por semana) → Todos son de Microsoft u OpenAI, y se comprueba su licencia.
- **[La verificación real es manual]** La CI no prueba la llamada real → Lo cubren los tests con transporte simulado y una verificación manual documentada en local y en el contenedor.

## Migration Plan

No hay datos que migrar. Para ejecutar el Runner de verdad:
- en local, `dotnet user-secrets set "DeepSeek:ApiKey" …` y `dotnet run`;
- en contenedor, `DEEPSEEK_API_KEY_FILE=<ruta> docker compose run --build --rm runner`.

Para deshacer el change, se revierte el commit. La CI vuelve a esperar el código 0 con el `ci.yml` anterior, porque los dos cambian juntos.

## Open Questions

- ¿Reenvío del razonamiento en turnos con herramientas cuando *thinking* está activado? No afecta a esta fase, porque está desactivado por defecto y no hay herramientas. Se resolverá en la fase 4.
