# AGENTS.md

Guía de contexto y comportamiento para cualquier asistente de IA que trabaje en este repositorio. Léela completa antes de modificar código.

## 1. Contexto y límites

- Este repo es un **sandbox de patrones de arquitectura agéntica** en .NET 10, no un ERP ni un producto de producción.
- El LLM es **DeepSeek** (API compatible con OpenAI), consumido vía `Microsoft.Extensions.AI` (`IChatClient`).
- El dominio es **dummy e in-memory**: simula lógica de negocio, validaciones, fallos inducidos y cambios de estado.
- **Fuera de alcance**: bases de datos reales, integración con ERP/CRM reales, autenticación de usuarios, UI, despliegue, alta disponibilidad.
- No añadas dependencias, proyectos ni capas "por si acaso". Si algo no sirve a un patrón que se quiere validar, no se añade.
- Nunca incluyas secretos, API keys ni datos reales en el repo. La key va en User Secrets o variables de entorno.

## 2. Principios de diseño

1. **Separación estricta**: las reglas de negocio viven en el dominio/aplicación; la invocación del LLM vive en `Sandbox.Agents`. El dominio no conoce `Microsoft.Extensions.AI`.
2. **El LLM propone, el dominio decide.** Ninguna regla de negocio se delega al prompt. El prompt guía; el código valida.
3. **CQRS simplificado**: separa Commands (cambian estado) de Queries (solo leen). Sin buses ni mediadores salvo necesidad demostrada. Las tools de lectura y de escritura son distintas.
4. **Inmutabilidad en DTOs**: `record` con propiedades `init`/posicionales, colecciones como `IReadOnlyList<T>`. El estado mutable queda encapsulado dentro de los servicios dummy.
5. **Guardrails como middleware** del pipeline de `IChatClient`/tools, no mezclados con la lógica de las tools.
6. **Determinismo**: todo lo que no sea la llamada al LLM debe ser reproducible. Los fallos inducidos se activan por configuración o semilla, nunca al azar sin control.
7. **Clean Code**: nombres que revelan intención, funciones pequeñas, sin comentarios que repitan el código, sin código muerto.
8. **Testeabilidad**: todo se puede probar con un `IChatClient` falso, sin red ni API key.

## 3. Convenciones de código C#

- Target `net10.0`, `LangVersion` por defecto del SDK.
- `<Nullable>enable</Nullable>` y `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (vía `Directory.Build.props`). Prohibido `!` (null-forgiving) salvo justificación en comentario.
- **Primary constructors** para servicios y clases con dependencias inyectadas.
- **`record`** (o `record struct`) para DTOs, comandos, queries y resultados.
- File-scoped namespaces, `sealed` por defecto en clases concretas, `var` cuando el tipo es evidente.
- Async de extremo a extremo; todo método async público acepta `CancellationToken` y lo propaga. Sufijo `Async`.
- Gestión de paquetes centralizada (`Directory.Packages.props`). No pongas versiones en los `.csproj`.
- Inyección de dependencias por constructor; sin service locator ni estáticos con estado.
- Logging con `ILogger<T>` y plantillas de mensaje (sin interpolación). No registres secretos ni payloads completos del LLM por defecto.
- Un tipo público por fichero; el nombre del fichero coincide con el tipo.
- Tests: xUnit, patrón Arrange/Act/Assert, nombres `Metodo_Condicion_Resultado`.

## 4. Reglas estrictas para Tools (`AIFunction`)

Las tools se crean con `AIFunctionFactory.Create` sobre métodos de clases dedicadas en `Sandbox.Agents/Tools`.

1. **Descripción obligatoria**: cada método de tool y **cada parámetro** lleva `[Description]` (`System.ComponentModel`), escrita para el LLM: qué hace, cuándo usarla, formato esperado, unidades.
2. **Nombres**: `snake_case` o `PascalCase` verbo-objeto, único y estable (`get_order`, `cancel_order`). No cambies nombres sin actualizar tests y prompts.
3. **Schema JSON limpio**:
   - Parámetros simples y tipados (`string`, `int`, `decimal`, `enum`, `Guid`). Usa `enum` en lugar de strings libres cuando el conjunto es cerrado.
   - Pocos parámetros (idealmente ≤ 4). Sin objetos anidados profundos ni tipos polimórficos.
   - Parámetros opcionales solo con valor por defecto explícito y documentado.
   - Verifica el schema generado (`AIFunction.JsonSchema`) en un test.
4. **Sin excepciones hacia el LLM**:
   - Una tool **nunca lanza** excepciones de negocio ni de validación. Devuelve un resultado tipado, por ejemplo:

     ```csharp
     public sealed record ToolResult<T>(bool Success, T? Data, ToolError? Error);
     public sealed record ToolError(string Code, string Message, bool Retryable);
     ```
   - `Code` es estable y determinista (`ORDER_NOT_FOUND`, `INSUFFICIENT_STOCK`, `INVALID_STATE_TRANSITION`). `Message` es breve, accionable y **no filtra** trazas, rutas ni detalles internos.
   - Las excepciones inesperadas se capturan en el borde de la tool (o en un middleware), se loguean con su detalle y se traducen a `ToolError { Code = "INTERNAL_ERROR", Retryable = false }`.
   - `OperationCanceledException` se propaga (no se convierte en error de tool).
5. **Validar antes de actuar**: la tool valida entradas (formato, rangos, existencia) y delega la regla en el servicio de dominio. Nunca asumas que el LLM envía datos correctos.
6. **Finas y sin lógica**: una tool solo mapea argumentos → comando/query → resultado. Cero reglas de negocio dentro.
7. **Seguridad de acciones**: las tools que cambian estado (Commands) se marcan explícitamente y pasan por los guardrails (autorización, límites, confirmación si es destructiva, auditoría). Las acciones irreversibles requieren confirmación explícita.
8. **Idempotencia**: los Commands aceptan una clave de idempotencia cuando haya riesgo de reintento por parte del modelo.
9. **Salidas compactas**: devuelve solo los campos que el LLM necesita; nada de volcar entidades completas.
10. **Tests obligatorios** por tool: caso feliz, cada código de error, y comprobación de schema/descripciones.

## 5. Guardrails

- Se implementan como `DelegatingChatClient` / filtros de invocación de funciones, registrados en el pipeline (`ChatClientBuilder`).
- Cubren: validación de entrada, límite de iteraciones del bucle de tool-calling, límite de tokens/coste, allow-list de tools, confirmación humana, auditoría estructurada.
- Un guardrail que bloquea devuelve un `ToolError` con código estable (`BLOCKED_BY_POLICY`), no una excepción.
- Cada guardrail tiene tests unitarios independientes del LLM.

## 6. OpenSpec

Este proyecto usa [OpenSpec](https://github.com/Fission-AI/OpenSpec) (spec-driven development). Las especificaciones en `openspec/specs/` son la fuente de verdad del comportamiento; el código se ajusta a ellas.

- **Antes de implementar** cualquier cambio de comportamiento, arquitectura o contrato de una tool, busca si existe un change o spec relacionado (`openspec list`, `openspec list --specs`).
- **Si no existe**, crea un change en `openspec/changes/<id-verbo-objeto>/` con `proposal.md` (por qué y qué cambia), `tasks.md` (checklist) y los spec deltas, y **espera la aprobación del usuario** antes de implementar.
- Los requisitos se redactan con `SHALL`/`MUST` y cada uno lleva al menos un `#### Scenario:` (Given/When/Then). Los contratos de tools (nombre, parámetros, códigos de error) y los guardrails se especifican como requisitos.
- Valida siempre con `openspec validate <id> --strict` antes de pedir revisión.
- Durante la implementación, ve marcando las tareas de `tasks.md`; no marques como hecho lo que no esté probado.
- Tras el despliegue/merge, archiva con `openspec archive <id>`, solo cuando el usuario lo pida.
- Correcciones de bugs que restauran el comportamiento ya especificado, typos y cambios de formato no necesitan change.
- `openspec/` se gestiona con la CLI; no edites a mano la estructura generada salvo `openspec/config.yaml`, specs y changes. Los skills y comandos generados (`.claude/`, `.agents/`) se regeneran con `openspec update`; no los edites a mano.
- En Claude Code el flujo se invoca con `/opsx:propose`, `/opsx:apply`, `/opsx:archive` y `/opsx:explore`. En otros asistentes, con los skills `openspec-*` de `.agents/skills/`.
- Una fase de `docs/plan.md` = uno o varios changes. Las decisiones de diseño van en `design.md` del change o en `docs/adr/` si trascienden un único change.

## 7. Flujo de trabajo para asistentes

- Trabaja por fases según `docs/plan.md`; no adelantes fases sin que se pida.
- Cambios pequeños y revisables; un cambio = un propósito.
- Antes de dar algo por terminado: `dotnet build` sin warnings y `dotnet test` en verde. Si no puedes ejecutarlo, dilo.
- Las decisiones de arquitectura no triviales se documentan en `docs/adr/`.
- Si una instrucción entra en conflicto con este documento, pregunta antes de actuar.
- No hagas commits, push ni cambios en CI sin petición explícita.
