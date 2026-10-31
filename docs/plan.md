# Plan de trabajo

| Fase | Nombre | Entregables | Criterio de salida |
|------|--------|-------------|--------------------|
| 0 | Bootstrap y documentación | `README.md`, `AGENTS.md`, este plan, `.gitignore`, documentación de aprendizaje base (`docs/learn/`) | Docs revisados y aprobados |
| 1 | Setup de solución | `openspec init` + `openspec/config.yaml` (contexto y reglas por artefacto), `global.json` (.NET 10), `Directory.Build.props` (nullable, warnings as errors), `Directory.Packages.props`, `.slnx`, proyectos vacíos, CI mínima (build + test) | `dotnet build` y `dotnet test` verdes |
| 2 | Infraestructura base | Host de consola, DI, configuración (User Secrets), logging, `IChatClient` hacia DeepSeek (`Microsoft.Extensions.AI.OpenAI` con endpoint personalizado), cliente falso para tests | "Hola mundo" contra DeepSeek y test equivalente con cliente falso |
| 3 | Servicios dummy | Dominio (Order, Customer, Inventory), servicios in-memory, Commands/Queries, inyector de fallos configurable | Tests de dominio, incluidos fallos y transiciones inválidas |
| 4 | Pipeline agéntico | Tools (`AIFunction`) con `ToolResult<T>`, `UseFunctionInvocation`, Runner, system prompt, trazas | Escenario feliz end-to-end (p. ej. crear pedido) |
| 5 | Guardrails | Middleware: límites de iteraciones/tokens, allow-list, autorización, confirmación humana, auditoría | Cada guardrail con tests; acciones bloqueadas devuelven error tipado |
| 6 | Casos de prueba y escenarios | Escenarios deterministas (fallo de stock, pedido inexistente, prompt injection, reintentos, acción destructiva) + suite opcional contra DeepSeek real | Matriz de escenarios documentada y ejecutable |
| 7 | Evaluación y cierre | Informe de hallazgos, ADRs, lista de patrones validados y descartados | Conclusiones de la PoC documentadas |

## Metodología

Desde la fase 1, cada fase se ejecuta con OpenSpec: uno o varios *changes* (`openspec/changes/<id>/`) con propuesta, tareas y spec deltas, validados con `openspec validate --strict` y archivados al completarse. Las fases 3 a 5 deben especificar como requisitos con escenarios los servicios de dominio, los contratos de las tools y los guardrails; los escenarios de la fase 6 se derivan de ellos.

## Documentación de aprendizaje paralela

Este laboratorio busca también **aprender** los conceptos que valida. Por eso la documentación de `docs/learn/` avanza en paralelo al código:

- Cada *change* de OpenSpec incluye una tarea de documentación de aprendizaje: crear o actualizar el documento que explica el concepto que se acaba de implementar o validar, con lo aprendido en la práctica (resultados, trazas, errores).
- Cada fase termina con una revisión: qué se ha aprendido, qué afirmaciones de los documentos ha desmentido la práctica y qué conceptos nuevos conviene documentar. El asistente propone esos temas nuevos y se registran en el backlog de [`docs/learn/README.md`](learn/README.md).
- El mapa de qué se documenta en cada fase está en [`docs/learn/README.md`](learn/README.md).
- Criterio de salida de cada fase: la documentación de aprendizaje correspondiente está al día.

## Riesgos a vigilar

- Soporte y calidad del *function calling* según el modelo de DeepSeek elegido.
- No determinismo del LLM: las pruebas críticas deben usar cliente falso.
- Coste y latencia: límites de iteraciones y tokens desde la fase 2.
