# Tasks

Rama de trabajo: `feat/add-deepseek-chat-client`. Referencias: `design.md` (decisiones D1 a D10 y experimentos del explore) y `specs/`.

**Verificaciones con la clave real** (grupos 4 y 5): la clave se lee de `%USERPROFILE%\.secrets\deepseek_apikey.txt` y nunca se imprime, se escribe en el repositorio ni aparece en el texto de un comando. Docker Desktop hace falta solo en el grupo 5; al terminar, salir de Docker Desktop y ejecutar `wsl --shutdown`.

## 1. Dependencias

- [x] 1.1 Reverificar en la API de NuGet la última versión estable de cada paquete y anotarla aquí con la fecha:
  - `Microsoft.Extensions.AI`;
  - `Microsoft.Extensions.AI.OpenAI`;
  - `OpenAI`;
  - `Microsoft.Extensions.Hosting`;
  - `Microsoft.Extensions.Configuration.KeyPerFile`.

  Comprobar la licencia de cada uno en su `.nuspec`. Si alguna no es MIT, Apache-2.0 u otra permisiva, parar y consultar.

  **Resultado (API de NuGet, `v3-flatcontainer`, 2026-11-01):** `Microsoft.Extensions.AI` y `.AI.OpenAI` 10.11.0; `OpenAI` 2.14.0; `Microsoft.Extensions.Hosting` y `.Configuration.KeyPerFile` 10.0.14. Todos declaran `<license type="expression">MIT</license>` en su `.nuspec`. `Microsoft.Extensions.AI.OpenAI` 10.11.0 depende de `OpenAI` 2.14.0, y este de `System.ClientModel` 1.15.0 (la última estable es 1.16.0, pero no se fija: llega como transitiva).
- [x] 1.2 Añadir las versiones a `Directory.Packages.props` y las referencias:
  - `Sandbox.Agents`: `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI` y `OpenAI`;
  - `Sandbox.Runner`: `Microsoft.Extensions.Hosting` y `Microsoft.Extensions.Configuration.KeyPerFile`.

  Añadir solo los paquetes que hagan falta para compilar; si alguno llega ya como dependencia transitiva, no se referencia. Verificar que `dotnet build` termina sin advertencias (incluida la auditoría de NuGet) y que `LayerRulesTests` sigue en verde.

## 2. Cliente de chat (`Sandbox.Agents`)

- [x] 2.1 Crear `DeepSeekOptions` y su validador (D2), con tests en `Sandbox.Agents.Tests` con nombres `Metodo_Condicion_Resultado`:
  - sin clave falla y el mensaje nombra `DeepSeek:ApiKey`;
  - un endpoint no HTTPS o relativo falla;
  - un modelo vacío falla;
  - opciones válidas pasan;
  - ningún mensaje de error contiene el valor de la clave usada en el test.
- [x] 2.2 Crear la construcción del `IChatClient` (D3) con el razonamiento desactivable mediante `JsonPatch`. `SCME0001` se suprime en un solo fichero, con comentario. Tests con transporte HTTP simulado (D6):
  - con las opciones por defecto, el JSON enviado lleva `model: deepseek-flash` y `thinking: {type: disabled}`;
  - con `Thinking=true`, el JSON no lleva `thinking`, y el `TextReasoningContent` de una respuesta grabada llega separado del texto;
  - la petición va a `<Endpoint>/chat/completions` con la cabecera `Authorization`. El test comprueba que existe, sin imprimirla.
- [x] 2.3 Crear `ChatProviderException` y el decorador que traduce los errores (D3). Tests con transporte simulado:
  - un 401 cuyo cuerpo contiene `****pike` da `ChatProviderException` con categoría `Authentication` y código 401, y ni su `Message` ni su `ToString()` contienen `pike`;
  - un 429 da `RateLimited`;
  - un fallo de conexión da `Network`;
  - una cancelación se propaga como `OperationCanceledException`.
- [x] 2.4 Crear el decorador de registro (D3). Tests con un `ILoggerProvider` de prueba:
  - una llamada correcta registra el modelo pedido, el que respondió, la duración y los tokens, y no registra el texto enviado ni el recibido;
  - si el `model` de la respuesta es distinto del pedido, se registra un aviso con los dos nombres;
  - ningún registro contiene la clave del test.
- [x] 2.5 Crear `FakeChatClient` en `tests/Sandbox.Agents.Tests/Fakes/` (D6). Puede programarse para responder un texto o para lanzar un `ChatProviderException`, y registra los mensajes recibidos. Tests de los dos escenarios de la spec. Borrar `AgentsAssemblyWiringTests`, ya sustituido por tests reales, y comprobar que el proyecto sigue teniendo tests.

## 3. Host del Runner (`Sandbox.Runner`)

- [x] 3.1 Crear el método que construye el host (D4), con el directorio de secretos como parámetro (por defecto `/run/secrets`), `appsettings.json` sin clave, `UserSecretsId` en el `.csproj` y `Properties/launchSettings.json` con `DOTNET_ENVIRONMENT=Development`. Los registros van a la salida de errores. Tests en `Sandbox.Scenarios.Tests` con un directorio temporal:
  - el fichero `DeepSeek__ApiKey` da la clave;
  - el fichero prevalece sobre la variable de entorno;
  - sin directorio arranca con la clave de otra fuente;
  - un salto de línea final en el fichero no llega a la clave usada. Anotar en `design.md` si *key-per-file* lo recorta o lo hace el código.
- [x] 3.2 Crear el saludo de comprobación y los códigos de salida (D5): `GreetingRunner`, `ExitCodes` y `Program.cs`. El resultado va a la salida estándar. Tests del flujo completo con `FakeChatClient` (enlazado según D6), uno por código:
  - 0: la salida muestra el texto, el modelo y los tokens;
  - 2: sin clave, el mensaje nombra la opción;
  - 3: con `ChatProviderException` de autenticación, el mensaje dice que la autenticación falló y no contiene `pike`;
  - 1: con una excepción inesperada, el mensaje no contiene la traza.
- [x] 3.3 Documentación: crear `docs/learn/10-configuracion-y-secretos-en-net.md`. Debe cubrir:
  - el host genérico;
  - las fuentes de configuración y su prioridad, con un diagrama;
  - los entornos y la trampa de las User Secrets fuera de `Development`;
  - *key-per-file* y su relación con los secretos de compose;
  - el patrón de opciones y `ValidateOnStart`;
  - por qué los mensajes de validación no incluyen valores;
  - la separación entre salida estándar y de errores;
  - los códigos de salida.

  Añadir lo observado en 3.1 y 3.2, contrastado con Microsoft Learn y con la fecha. Añadir los términos al glosario. Verificar con `check-docs.ps1`.

## 4. Verificación real en local

- [x] 4.1 Guardar la clave en User Secrets leyéndola del fichero, sin que aparezca en el comando ni en la salida. Ejecutar `dotnet run --project src/Sandbox.Runner` y verificar:
  - código 0;
  - la salida muestra la respuesta, `deepseek-flash` y los tokens;
  - los registros (en la salida de errores) no contienen la respuesta completa ni la clave.

  Anotar la latencia y los tokens.
- [x] 4.2 Verificar los fallos reales:
  - con `DOTNET_ENVIRONMENT=Production` y la clave solo en User Secrets, código 2;
  - con `DeepSeek__ApiKey` de una clave inválida en una variable de entorno de ese proceso, código 3, sin fragmentos de la clave en la salida ni en los registros;
  - con `DeepSeek__Model=deepseek-chat`, el registro avisa de que respondió `deepseek-flash`.
- [x] 4.3 Documentación: actualizar `docs/learn/04-herramientas-microsoft.md`:
  - resolver el "(A verificar en la fase 2)" con el endpoint, `AsIChatClient`, `RawRepresentationFactory` y `JsonPatch`;
  - `TextReasoningContent`, los modelos actuales y su remapeo;
  - la caché de *prompt*;
  - el nombre que genera `AIFunctionFactory` sin nombre explícito;
  - el 401 que repite parte de la clave.

  Incluir lo medido en 4.1 y 4.2. Verificar con `check-docs.ps1`.

## 5. Contenedor

- [x] 5.1 Actualizar `compose.yaml` (D7): quitar `network_mode: none` y declarar el secreto con `target: DeepSeek__ApiKey` y `file: ${DEEPSEEK_API_KEY_FILE:-./secrets/deepseek_api_key}`. Verificar:
  - `docker compose config --quiet` no da errores;
  - con `DEEPSEEK_API_KEY_FILE` apuntando al fichero del responsable, `docker compose run --build --rm runner` devuelve 0 y muestra la respuesta;
  - dentro del contenedor el secreto está en `/run/secrets/DeepSeek__ApiKey` y es legible por el usuario 1654 (comprobarlo con el código de salida y, si falla, con `docker inspect`, porque la imagen no tiene shell).

  Anotar en `design.md` si hizo falta `tmpfs` o ajustar permisos.
- [x] 5.2 Verificar el endurecimiento y la ausencia de la clave:
  - con un contenedor sin `--rm`, `docker inspect` muestra `ReadonlyRootfs`, `CapDrop` y `no-new-privileges`, y ni las `Env` ni `Config` contienen la clave. Buscarla comparando el `inspect` con el fichero, sin imprimirla;
  - con `DEEPSEEK_API_KEY_FILE` apuntando a un fichero inexistente, el Runner devuelve 2;
  - `docker build --target test .` está en verde;
  - con un canario en `secrets/` y otro en `.env`, ni la imagen de test ni la final los contienen.

  Borrar los contenedores y los canarios.
- [x] 5.3 Actualizar el paso de ejecución de prueba del job `image` en `.github/workflows/ci.yml` (D8) para que espere el código 2 y que la salida nombre `DeepSeek:ApiKey`. Verificar en local que ese comando, con las mismas opciones de `docker run`, devuelve 2 y pasa la comprobación. También que actionlint, fijado por digest, no da errores.
- [x] 5.4 Documentación: en el documento 07, el secreto real montado como fichero, la red del `runner`, lo observado en 5.1 y 5.2, y el nuevo comportamiento de la CI. Actualizar el `README.md`:
  - modelo `deepseek-flash`;
  - User Secrets con `dotnet run`, que fija `Development`;
  - ejecución en contenedor con `DEEPSEEK_API_KEY_FILE`;
  - códigos de salida.

  Verificar que los comandos del `README.md` funcionan tal como están escritos.

## 6. Integración y cierre

- [x] 6.1 Actualizar `docs/learn/README.md` (índice, fila de la fase 2 y backlog) y `docs/plan.md` (estado de la fase 2). Revisar si `AGENTS.md` (secciones 1 y 10) necesita mencionar el secreto por fichero o los códigos de salida. Verificar con `check-docs.ps1`.
- [x] 6.2 Validar y cerrar:
  - `openspec validate --all --strict` sin errores;
  - `dotnet build` sin advertencias y `dotnet test` en verde, sin red ni clave;
  - `docker build --target test .` en verde;
  - revisión de que ningún fichero versionado contiene la clave (búsqueda por su contenido, sin imprimirlo), secretos ni rutas locales.

## Workflow follow-up

- `/opsx:verify` sin problemas críticos y `/opsx:archive` en esta misma rama, antes de abrir el PR. Commits, push y PR solo cuando el usuario lo pida. En el PR, los checks `test`, `image` (ahora espera el código 2) y `openspec` deben estar en verde.
- Fase 4: resolver si con el razonamiento activado hay que reenviarlo en los turnos con herramientas, y dar nombres explícitos a las tools.
