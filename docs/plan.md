# Plan de trabajo

| Fase | Nombre | Entregables | Criterio de salida |
|------|--------|-------------|--------------------|
| 0 | Bootstrap y documentación | `README.md`, `AGENTS.md`, este plan, `.gitignore`, documentación de aprendizaje base (`docs/learn/`) | Docs revisados y aprobados |
| 1 | Setup de solución (tres changes, ver más abajo) | `openspec init` + `openspec/config.yaml` (contexto y reglas por artefacto), `global.json` (.NET 10), `Directory.Build.props` (nullable, warnings as errors), `Directory.Packages.props`, `.slnx`, proyectos vacíos, `.gitattributes` (`eol=lf`), `Dockerfile` multi-etapa (restore, build, test, publish y runtime sin root), `.dockerignore`, `compose.yaml`, CI sobre el mismo `Dockerfile` (build y test en contenedor), actualizaciones automáticas de dependencias con Dependabot, medición nativo frente a contenedor | `dotnet build` y `dotnet test` verdes, `docker build --target test` verde en CI y check de CI exigido en el ruleset de `main` |
| 2 | Infraestructura base | Host de consola, DI, configuración (User Secrets), logging, `IChatClient` hacia DeepSeek (`Microsoft.Extensions.AI.OpenAI` con endpoint personalizado), cliente falso para tests | "Hola mundo" contra DeepSeek, también dentro del contenedor con la API key como secreto por fichero, y test equivalente con cliente falso |
| 3 | Servicios dummy | Dominio (Order, Customer, Inventory), servicios in-memory, Commands/Queries, inyector de fallos configurable | Tests de dominio, incluidos fallos y transiciones inválidas |
| 4 | Pipeline agéntico | Tools (`AIFunction`) con `ToolResult<T>`, `UseFunctionInvocation`, Runner, system prompt, trazas | Escenario feliz end-to-end (p. ej. crear pedido) |
| 5 | Guardrails | Middleware: límites de iteraciones/tokens, allow-list, autorización, confirmación humana, auditoría, panel de trazas OTLP opcional (Aspire Dashboard como perfil de compose) | Cada guardrail con tests; acciones bloqueadas devuelven error tipado |
| 6 | Casos de prueba y escenarios | Escenarios deterministas (fallo de stock, pedido inexistente, prompt injection, reintentos, acción destructiva) + suite opcional contra DeepSeek real | Matriz de escenarios documentada y ejecutable |
| 7 | Evaluación y cierre | Informe de hallazgos (incluidos tamaño de imagen, endurecimiento y resultado del experimento de contenedores), ADRs, lista de patrones validados y descartados | Conclusiones de la PoC documentadas |

## Metodología

Desde la fase 1, cada fase se ejecuta con OpenSpec: uno o varios *changes* (`openspec/changes/<id>/`) con propuesta, tareas y spec deltas, validados con `openspec validate --strict` y archivados al completarse. Las fases 3 a 5 deben especificar como requisitos con escenarios los servicios de dominio, los contratos de las tools y los guardrails; los escenarios de la fase 6 se derivan de ellos.

## Fase 1: tres changes

La fase 1 se divide en tres changes de OpenSpec, para que cada PR sea pequeño y revisable:

| Change | Contenido | Estado |
|--------|-----------|--------|
| `setup-dotnet-solution` | `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, solución `.slnx`, proyectos de código y de tests, test de reglas de capas | Fusionado en `main` |
| `add-container-build` | `Dockerfile` multi-etapa, `.dockerignore`, `compose.yaml`, `.gitattributes`, globalización invariante (ADR 0001) | Fusionado en `main` |
| `add-ci-pipeline` | Workflow de CI sobre el mismo `Dockerfile` (checks `test`, `image` con escaneo Grype y `openspec`), Dependabot, checks obligatorios en el ruleset de `main`, acciones fijadas por SHA, workflow manual de medición nativo frente a contenedor (ADR 0002) | Fusionado en `main`; medición hecha en un runner (documento 07) |

Los entregables de la tabla de fases se reparten entre ellos; el criterio de salida de la fase se cumple cuando los tres están hechos.

## Fase 2: un change

| Change | Contenido | Estado |
|--------|-----------|--------|
| `add-deepseek-chat-client` | Host genérico en el Runner (configuración por capas con User Secrets y secretos por fichero, validación al arrancar, códigos de salida 0/1/2/3), `IChatClient` hacia DeepSeek (`deepseek-flash` sin razonamiento por defecto, errores del proveedor sin fragmentos de la clave, registro de modelo y tokens), cliente falso y transporte HTTP simulado para tests, `compose.yaml` con red y la API key como secreto, CI que espera el código 2 sin clave | Fusionado en `main` (PR #15) |

Criterio de salida cumplido: saludo real contra DeepSeek con `dotnet run` y con `docker compose run`, y tests equivalentes con el cliente falso (`RunnerExitCodeTests`).

## Fase 3: un change

| Change | Contenido | Estado |
|--------|-----------|--------|
| `add-order-domain` | Dominio de ventas (pedidos con máquina de estados `Placed -> Shipped/Cancelled`, clientes, stock con reservas, límites de negocio), errores como resultado con código estable y tipo (ADR 0003), commands `PlaceOrder`/`CancelOrder`/`ShipOrder` con clave de idempotencia y queries `GetOrder`/`GetCustomer`/`GetStock` sobre un estado in-memory con catálogo semilla y reloj inyectado, inyector de fallos configurable (siempre, primeras N llamadas o probabilidad con semilla; antes o después de ejecutar; latencia), registro en el Runner con validación al arrancar (código 2), proyecto `Sandbox.Application.Tests` | Implementado, verificado en local y en contenedor, y archivado en la rama del change |

Criterio de salida: tests de dominio y de aplicación con todas las reglas, cada transición inválida, la idempotencia, la concurrencia y los fallos inducidos (antes y después de ejecutar, y con latencia sobre un reloj falso). Las sagas multipaso quedan para un dominio ampliado más adelante.

## Contenedores

Lo que se desarrolla se construye, prueba y ejecuta también en contenedor. Decisiones:

- **Flujo híbrido**: `dotnet` local para iterar rápido; el contenedor es la referencia para dar algo por terminado y la base de la CI. Un flujo "solo contenedor" se evalúa como experimento acotado en la fase 1 (medición documentada en [`docs/learn/07-contenedores-en-sistemas-agenticos.md`](learn/07-contenedores-en-sistemas-agenticos.md)).
- **`Dockerfile` explícito** multi-etapa, con etapa `test` sin red, usuario no root e imágenes fijadas. La vía de contenedores del SDK queda como comparación posible.
- **Secretos** por fichero montado, nunca en la imagen ni versionados.
- **Motor**: Docker o Podman (se evitan funciones exclusivas de uno solo). Ya está instalado Docker Desktop con WSL2 acotado en el equipo actual; límites y valores en el documento 07.
- Fuera de alcance de momento: dev container y servicios adicionales distintos del panel de trazas.

## Documentación de aprendizaje paralela

Este laboratorio busca también **aprender** los conceptos que valida. Por eso la documentación de `docs/learn/` avanza en paralelo al código:

- Cada *change* de OpenSpec incluye una tarea de documentación de aprendizaje: crear o actualizar el documento que explica el concepto que se acaba de implementar o validar, con lo aprendido en la práctica (resultados, trazas, errores).
- Cada fase termina con una revisión: qué se ha aprendido, qué afirmaciones de los documentos ha desmentido la práctica y qué conceptos nuevos conviene documentar. El asistente propone esos temas nuevos y se registran en el backlog de [`docs/learn/README.md`](learn/README.md).
- El mapa de qué se documenta en cada fase está en [`docs/learn/README.md`](learn/README.md).
- Criterio de salida de cada fase: la documentación de aprendizaje correspondiente está al día.

## Riesgos a vigilar

- Soporte y calidad del *function calling* según el modelo de DeepSeek elegido. En el explore de la fase 2, una *tool call* funcionó con `deepseek-flash`, con razonamiento y sin él; la calidad con tools reales se medirá en la fase 4.
- DeepSeek cambia modelos y nombres sin aviso (en la fase 2, `deepseek-chat` ya respondía como `deepseek-flash`). El modelo es configurable y el Runner avisa del remapeo en el registro.
- No determinismo del LLM: las pruebas críticas deben usar cliente falso.
- Coste y latencia: desde la fase 2 se registran los tokens y la duración de cada llamada; los límites de iteraciones y tokens llegan con los guardrails de la fase 5.
- Recursos del equipo de desarrollo actual (Surface Go 2: 2 núcleos de baja potencia y 8 GB de RAM). Docker Desktop con WSL2 en reposo consume unos 2,8 GB y deja unos 0,5 GB libres. Por eso Docker se usa solo para construir y verificar, se cierra al terminar (`wsl --shutdown`), y la medición nativo frente a contenedor se hace en otro equipo. La CI valida la imagen en cualquier caso. Detalle en [`docs/learn/07-contenedores-en-sistemas-agenticos.md`](learn/07-contenedores-en-sistemas-agenticos.md).
