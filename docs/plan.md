# Plan de trabajo

| Fase | Nombre | Entregables | Criterio de salida |
|------|--------|-------------|--------------------|
| 0 | Bootstrap y documentación | `README.md`, `AGENTS.md`, este plan, `.gitignore` | Docs revisados y aprobados |
| 1 | Setup de solución | `openspec init` + `openspec/project.md` (contexto, stack y convenciones), `global.json` (.NET 10), `Directory.Build.props` (nullable, warnings as errors), `Directory.Packages.props`, `.slnx`, proyectos vacíos, CI mínima (build + test) | `dotnet build` y `dotnet test` verdes |
| 2 | Infraestructura base | Host de consola, DI, configuración (User Secrets), logging, `IChatClient` hacia DeepSeek (`Microsoft.Extensions.AI.OpenAI` con endpoint personalizado), cliente falso para tests | "Hola mundo" contra DeepSeek y test equivalente con cliente falso |
| 3 | Servicios dummy | Dominio (Order, Customer, Inventory), servicios in-memory, Commands/Queries, inyector de fallos configurable | Tests de dominio, incluidos fallos y transiciones inválidas |
| 4 | Pipeline agéntico | Tools (`AIFunction`) con `ToolResult<T>`, `UseFunctionInvocation`, Runner, system prompt, trazas | Escenario feliz end-to-end (p. ej. crear pedido) |
| 5 | Guardrails | Middleware: límites de iteraciones/tokens, allow-list, autorización, confirmación humana, auditoría | Cada guardrail con tests; acciones bloqueadas devuelven error tipado |
| 6 | Casos de prueba y escenarios | Escenarios deterministas (fallo de stock, pedido inexistente, prompt injection, reintentos, acción destructiva) + suite opcional contra DeepSeek real | Matriz de escenarios documentada y ejecutable |
| 7 | Evaluación y cierre | Informe de hallazgos, ADRs, lista de patrones validados y descartados | Conclusiones de la PoC documentadas |

## Metodología

Desde la fase 1, cada fase se ejecuta con OpenSpec: uno o varios *changes* (`openspec/changes/<id>/`) con propuesta, tareas y spec deltas, validados con `openspec validate --strict` y archivados al completarse. Las fases 3 a 5 deben especificar como requisitos con escenarios los servicios de dominio, los contratos de las tools y los guardrails; los escenarios de la fase 6 se derivan de ellos.

## Riesgos a vigilar

- Soporte y calidad del *function calling* según el modelo de DeepSeek elegido.
- No determinismo del LLM: las pruebas críticas deben usar cliente falso.
- Coste y latencia: límites de iteraciones y tokens desde la fase 2.
