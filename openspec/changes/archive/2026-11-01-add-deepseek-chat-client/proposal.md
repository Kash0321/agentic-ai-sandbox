# Proposal

## Why

La fase 1 dejó una solución que se construye, se prueba y se ejecuta en contenedor, pero el Runner solo hace `return 0;`. Todavía no hay host, configuración, logging ni conexión con el modelo. Sin eso no se puede empezar el dominio (fase 3) ni el pipeline agéntico (fase 4).

Es la **fase 2** de `docs/plan.md` (*Infraestructura base*), en un único change. Su criterio de salida es un "hola mundo" contra DeepSeek, también dentro del contenedor con la API key como secreto por fichero, y un test equivalente con un cliente falso.

El change parte de un explore con llamadas reales a la API de DeepSeek; los resultados están en `design.md`. Varios hallazgos condicionan el alcance:

- **Los modelos de DeepSeek han cambiado.** La documentación actual solo lista `deepseek-flash` y `deepseek-v4-pro`. `deepseek-chat`, que citan el `README.md` y el documento 04, se acepta pero acaba atendido por `deepseek-flash`, y lo mismo pasa con `deepseek-reasoner`.
- **El modo *thinking* está activado por defecto.** Desactivarlo exige enviar un campo que no es de OpenAI, y para eso hay que usar una API experimental del SDK.
- **El error 401 de DeepSeek repite los 4 últimos caracteres de la clave.** Registrar ese mensaje tal cual filtraría parte de la clave.
- **Las User Secrets solo se cargan en el entorno `Development`.** Las instrucciones actuales del `README.md` no funcionarían en una consola, que por defecto corre en `Production`.

## What Changes

- **Host de consola** en `Sandbox.Runner` con el host genérico de .NET: DI, logging y configuración por capas (`appsettings.json`, User Secrets en `Development`, variables de entorno y ficheros de secretos montados en el contenedor).
- **Opciones de DeepSeek** (endpoint, modelo, modo de razonamiento y clave), validadas al arrancar. Por defecto: `deepseek-flash` con el razonamiento desactivado.
- **`IChatClient` hacia DeepSeek** en `Sandbox.Agents`: el SDK de OpenAI apuntando al endpoint de DeepSeek, con el adaptador de `Microsoft.Extensions.AI`. Para tests hay un **cliente falso** sin red.
- **Saludo de comprobación**: el Runner envía un mensaje fijo, muestra la respuesta, el modelo que respondió y los tokens, y termina. Cada fallo tiene un **código de salida** y un mensaje claro que nunca incluye la clave ni el texto de error del proveedor.
- **Contenedor**:
  - el servicio `runner` de compose pasa a tener red y declara la API key como **secreto por fichero**, cuya ruta se puede elegir con una variable de entorno;
  - la imagen sigue sin secretos.
  - **BREAKING** para la CI: sin clave, el Runner termina con el código de "configuración inválida" en lugar de 0. El check `image` comprueba ese código.
- **Documentación**:
  - documento 04: lo comprobado del adaptador y de DeepSeek;
  - documento 07: el secreto real y la red del `runner`;
  - documento nuevo `10-configuracion-y-secretos-en-net.md`;
  - glosario, `README.md` (modelos actuales, User Secrets, ejecución con secreto) y `docs/plan.md`.

### Fuera de alcance

- Tools (`AIFunction`), bucle de *tool calling* en el Runner, system prompt y escenarios: fase 4. El explore probó una *tool call* solo para medir el adaptador.
- Guardrails (límites de tokens y de iteraciones, auditoría) y control de salida de red del `runner` a un único dominio: fase 5.
- Trazas OpenTelemetry y panel de trazas: fase 5.
- Reintentos, timeouts afinados y *fallback* entre modelos.
- Ejecutar la llamada real a DeepSeek en la CI: la CI no tiene secretos, y la verificación real es manual.
- *Streaming* de respuestas.

## Capabilities

### New Capabilities

- `chat-client`: cómo se conecta la solución a un modelo de lenguaje. Cubre:
  - las opciones de conexión y su validación;
  - el modelo y el modo de razonamiento configurables;
  - el registro del `IChatClient` hacia DeepSeek;
  - el cliente falso para tests;
  - el registro de llamadas sin secretos ni contenidos;
  - el tratamiento de los errores del proveedor sin filtrar la clave.
- `runner-host`: cómo arranca y termina el Runner. Cubre:
  - las fuentes de configuración y su prioridad, incluidos los secretos por fichero;
  - el saludo de comprobación;
  - los códigos de salida.

### Modified Capabilities

- `container-build`:
  - "Imagen final mínima y no root": sin clave, el Runner termina con el código de configuración inválida;
  - "Ejecución endurecida del Runner": se ejecuta con la clave como secreto;
  - "Runner sin red ni secretos declarados": se sustituye por el requisito de red y secreto por fichero.
- `continuous-integration`, "Imagen final verificada": el check `image` espera el código de configuración inválida, porque la CI no tiene clave.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: opciones y registro del cliente;
  - en `src/Sandbox.Runner/`: host, saludo, `appsettings.json` y `Properties/launchSettings.json`;
  - tests en `tests/Sandbox.Agents.Tests` y `tests/Sandbox.Scenarios.Tests`;
  - `docs/learn/10-…`.
- **Ficheros modificados**:
  - `Directory.Packages.props`;
  - los `.csproj` de `Sandbox.Agents` y `Sandbox.Runner`;
  - `src/Sandbox.Runner/Program.cs`;
  - `compose.yaml` y `.github/workflows/ci.yml`;
  - `README.md`, `docs/learn/04`, `docs/learn/07`, `docs/learn/glosario.md`, `docs/learn/README.md` y `docs/plan.md`.
- **Dependencias nuevas**, todas con licencia MIT **(a verificar en el apply)**, en la última versión estable comprobada en NuGet al implementar:
  - `Microsoft.Extensions.AI` y `Microsoft.Extensions.AI.OpenAI` (10.11.0 el 2026-11-01);
  - `OpenAI` (2.14.0);
  - `Microsoft.Extensions.Hosting` y `Microsoft.Extensions.Configuration.KeyPerFile` (10.0.14).

  Las librerías de IA solo entran en `Sandbox.Agents` y `Sandbox.Runner`, como exige `solution-layout`.
- **Código experimental**: desactivar el razonamiento usa `ChatCompletionOptions.Patch` (diagnóstico `SCME0001`). Se suprime en un único fichero y con justificación.
- **Red y coste**: el `runner` deja de estar aislado de la red; el control de salida estricto llega en la fase 5. Cada saludo real cuesta unos 15 tokens (fracciones de céntimo).
- **Flujo de trabajo**: para ejecutar el Runner de verdad hace falta una clave de DeepSeek, en User Secrets en local y como fichero en el contenedor. Los tests y la CI siguen sin red ni clave.
- **Conceptos nuevos y dónde se documentan**:
  - documento 10 (nuevo): host genérico de .NET, configuración por capas y su prioridad, entornos, User Secrets, proveedor *key-per-file*, patrón de opciones con validación al arrancar, y códigos de salida de una consola;
  - documento 04: adaptador OpenAI hacia un endpoint compatible, `RawRepresentationFactory` y `JsonPatch`, modos de razonamiento, `TextReasoningContent`, caché de *prompt* y remapeo de modelos;
  - documento 07: secretos de compose montados como ficheros y red del `runner`.
