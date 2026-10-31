# AGENTS.md

Guía de contexto y comportamiento para cualquier asistente de IA que trabaje en este repositorio. Léela completa antes de modificar código.

## 1. Contexto y límites

- Este repo es un **sandbox de patrones de arquitectura agéntica** en .NET 10, no un ERP ni un producto de producción.
- El LLM es **DeepSeek** (API compatible con OpenAI), consumido vía `Microsoft.Extensions.AI` (`IChatClient`).
- El dominio es **dummy e in-memory**: simula lógica de negocio, validaciones, fallos inducidos y cambios de estado.
- **Fuera de alcance**: bases de datos reales, integración con ERP/CRM reales, autenticación de usuarios, UI, despliegue, alta disponibilidad.
- No añadas dependencias, proyectos ni capas "por si acaso". Si algo no sirve a un patrón que se quiere validar, no se añade.
- Nunca incluyas secretos, API keys ni datos reales en el repo. La key va en User Secrets o variables de entorno.
- El proyecto es open source bajo licencia **MIT** (`LICENSE`). Las dependencias nuevas deben tener una licencia compatible (MIT, Apache-2.0, BSD u otra permisiva). No añadas dependencias con copyleft fuerte (GPL, AGPL) ni sin licencia clara sin consultarlo antes. No copies código de terceros sin comprobar su licencia.

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
- **Cierre del change, antes del PR**: con todas las tareas hechas y los tests en verde (ver la definición de terminado en la sección 8), ejecuta `/opsx:verify` y corrige lo crítico; después archiva con `/opsx:archive` (que sincroniza las specs principales) **en la misma rama**. El PR se abre con el change ya archivado: no se abre el PR de un change sin verificar y archivar. Si se te pide abrirlo antes, recuérdalo y pregunta.
- Correcciones de bugs que restauran el comportamiento ya especificado, typos y cambios de formato no necesitan change.
- `openspec/` se gestiona con la CLI; no edites a mano la estructura generada salvo `openspec/config.yaml`, specs y changes. Los skills y comandos generados (`.claude/`, `.agents/`) se regeneran con `openspec update`; no los edites a mano.
- En Claude Code el flujo se invoca con `/opsx:propose`, `/opsx:apply`, `/opsx:archive` y `/opsx:explore`. En otros asistentes, con los skills `openspec-*` de `.agents/skills/`.
- Una fase de `docs/plan.md` = uno o varios changes. Las decisiones de diseño van en `design.md` del change o en `docs/adr/` si trascienden un único change.

## 7. Documentación de aprendizaje

Este proyecto es un laboratorio cuyo objetivo incluye que su responsable **aprenda** los conceptos que valida. La documentación de `docs/learn/` se escribe **en paralelo al código**, no después. El índice, las convenciones y el backlog de temas están en `docs/learn/README.md`.

- **Cada change de OpenSpec incluye una tarea de documentación de aprendizaje**: crear o actualizar el documento de `docs/learn/` que cubre el concepto que introduce o valida el change. Un change no se da por terminado sin ella.
- La `proposal.md` de cada change indica qué conceptos nuevos introduce y qué documento de `docs/learn/` los cubre.
- **Enfoque didáctico**: explica qué es, por qué importa, cómo funciona, qué alternativas hay y cómo se aplica *en este proyecto* (con enlaces a specs y código). Incluye errores típicos y referencias.
- **Rigor**: contrasta con documentación oficial las afirmaciones sobre tecnología de terceros y anota la fecha de verificación. Marca como **(no verificado)** lo que no hayas podido comprobar. Distingue lo que dice la documentación de lo que es criterio de diseño propio. No inventes cifras, versiones ni comportamientos.
- **Aprendizaje real**: al implementar, incorpora lo observado (trazas, errores, costes). Si la práctica desmiente un documento, corrígelo y dilo.
- **Diagramas**: Mermaid cuando sea posible. En ASCII, solo caracteres monoespaciados seguros (`+ - | > < ^ v` y los de caja `┌ ─ ┐ │ └ ┘ ┬ ┴ ├ ┤ ┼`). Nunca `▶ ◀ ▲ ▼ →` dentro de dibujos: su ancho varía según la fuente y los descuadra.
- **Glosario**: añade a `docs/learn/glosario.md` los términos nuevos.
- **Propón temas nuevos**: al cerrar cada fase, sugiere los conceptos que merezca la pena documentar y regístralos en el backlog de `docs/learn/README.md`. Si durante el trabajo surge un concepto que el usuario probablemente no domina, dilo y propón documentarlo.
- La documentación de aprendizaje no sustituye a las specs: las specs dicen *qué debe hacer* el sistema; `docs/learn/` explica *por qué y cómo funciona*.

## 8. Flujo de trabajo para asistentes

- Trabaja por fases según `docs/plan.md`; no adelantes fases sin que se pida.
- Cambios pequeños y revisables; un cambio = un propósito.
- Antes de dar algo por terminado: `dotnet build` sin warnings y `dotnet test` en verde y, en cuanto exista el `Dockerfile`, también `docker build --target test` (ver sección 10). Si no puedes ejecutarlo, dilo.
- **Definición de terminado de un change** (condición para abrir su PR). Todo lo siguiente, sin excepciones silenciosas:
  1. Todas las tareas de `tasks.md` marcadas, y solo si están probadas.
  2. `openspec validate --all --strict` sin errores.
  3. `dotnet build` sin advertencias y `dotnet test` con **todos los tests automáticos en verde**; y, en cuanto exista el `Dockerfile`, `docker build --target test`.
  4. Documentación de aprendizaje al día (sección 7).
  5. Change verificado con `/opsx:verify`, sin problemas críticos.
  6. Change archivado con `/opsx:archive`, en la misma rama, con las specs principales sincronizadas.

  Si algo no se puede ejecutar o falla, no se da por terminado: dilo y explica qué falta.
- Las decisiones de arquitectura no triviales se documentan en `docs/adr/`.
- Si una instrucción entra en conflicto con este documento, pregunta antes de actuar.
- No hagas commits, push ni cambios en CI sin petición explícita.

## 9. Flujo de Git

`main` es la rama estable y está **protegida** en GitHub mediante un ruleset (`.github/rulesets/protect-main.json`: PR obligatorio con solo *squash*, sin borrado, sin force push, historial lineal y conversaciones resueltas). No se trabaja directamente sobre ella. Si se cambia el fichero, hay que reaplicarlo con `gh api`; el estado real manda sobre el JSON.

- **Una rama por cambio**: al empezar cualquier cambio (un change de OpenSpec, una corrección de documentación, una tarea de mantenimiento), crea una rama nueva desde `main` actualizada. Un change de OpenSpec = una rama.
- **Nombre de la rama**: `<tipo>/<descripcion-corta-en-kebab-case>`, con tipo `feat`, `fix`, `docs`, `chore` o `refactor`. Si hay change de OpenSpec, usa su id (por ejemplo `feat/add-order-service`).
- **Nunca commitees ni hagas push directo a `main`**, ni `push --force` sobre ramas compartidas.
- **Commits**: pequeños, con mensaje en inglés en modo imperativo y una línea de resumen clara. Solo cuando el usuario lo pida.
- **Pull request**: la integración en `main` se hace siempre mediante PR, abierto solo cuando el usuario lo pida. La descripción incluye qué cambia y por qué, el change de OpenSpec asociado (si lo hay) y cómo se ha verificado (build, tests, `openspec validate`). **El PR solo se abre cuando se cumple la definición de terminado de la sección 8**, incluidos el change verificado y archivado y todos los tests automáticos en verde. Una vez exista la CI, el PR no se considera listo hasta que todos sus checks estén en verde.
- **Merge**: lo hace el usuario, preferiblemente con *squash* para mantener un historial lineal, y solo con todos los checks en verde (el ruleset exigirá los de la CI cuando existan). No fusiones un PR sin que el usuario lo pida expresamente.
- **Tras el merge**: se borra la rama y se actualiza `main` en local antes de empezar el siguiente cambio.
- **Archivado en OpenSpec**: se hace en la rama del change, tras verificarlo y antes de abrir el PR (sección 6), nunca después del merge. Si el archivado regenera ficheros ajenos al change (por ejemplo, metadatos de los skills generados), no se incluyen en el commit: van a su propia rama de mantenimiento.

## 10. Contenedores

Lo que se desarrolla se construye, prueba y ejecuta también en contenedor. El detalle y las razones están en `docs/learn/07-contenedores-en-sistemas-agenticos.md`.

- **Flujo híbrido**: `dotnet` local para iterar rápido; el contenedor es la referencia para dar algo por terminado y la base de la CI. Si algo funciona en local y falla en el contenedor, el contenedor manda.
- **`Dockerfile` multi-etapa**, en este orden: `restore` (solo ficheros de dependencias, para cachear), `build`, `test`, `publish` y `final`. La etapa `test` no necesita red: los tests usan un `IChatClient` falso.
- **Imágenes base**: fijadas por tag explícito (nunca `latest`) y, cuando sea posible, por digest. Imagen final basada en el runtime, no en el SDK.
- **Usuario no root** en la imagen final. Sistema de ficheros de solo lectura, `cap_drop: ALL` y `no-new-privileges` en compose salvo motivo justificado.
- **Secretos**: nunca en la imagen, en `ARG`, en `ENV` del `Dockerfile`, en compose ni en ficheros versionados. La API key se pasa con un secreto por fichero (`secrets/`, ignorado por Git) o, como alternativa simple, por variable de entorno en tiempo de ejecución. Solo se versiona `.env.example`. El repositorio es público.
- **`.dockerignore` estricto** (`bin/`, `obj/`, `.git`, `secrets/`, `.env`, etc.).
- **Fin de línea**: `.gitattributes` fuerza `eol=lf` en `Dockerfile`, scripts y YAML, para evitar `/bin/sh^M` en Linux.
- **Compose**: el servicio `runner` es *one-shot* (`docker compose run --rm runner --scenario <id>`). El panel de trazas OTLP (Aspire Dashboard) es un perfil opcional y solo para desarrollo.
- **Motor**: compatible con Docker y Podman; evita funciones exclusivas de uno solo.
- **CI**: construye el mismo `Dockerfile`, con permisos mínimos y acciones fijadas por SHA. Su check es obligatorio en el ruleset de `main` cuando exista.
- **Datos**: nunca se montan datos reales en un contenedor; solo datos sintéticos.
- **Un contenedor no es un guardrail**: no sustituye a la autorización ni a las políticas de las secciones 4 y 5.
- Los cambios en el `Dockerfile` o en compose siguen el flujo normal (change de OpenSpec, rama y PR) y actualizan `docs/learn/07-contenedores-en-sistemas-agenticos.md` con lo aprendido.

## 11. Versiones y dependencias

- **Plataforma**: .NET 10 (LTS). Pasar a una versión mayor posterior solo cuando sea estable y oficial, mediante un change propio.
- **Al fijar la versión** de un paquete NuGet, una imagen base o una acción de GitHub, usa la **última versión estable oficial**, comprobada en la fuente primaria (la API de NuGet, `releases.json` de .NET, el registro de imágenes de Microsoft o las releases de GitHub). **No uses versiones preliminares (preview, RC)** sin una decisión expresa del usuario. Anota la fecha de verificación en el change o el PR.
- **No leas versiones de páginas de documentación**: pueden estar desfasadas (en este proyecto ya pasó con `Microsoft.Extensions.AI` y `Microsoft.Agents.AI`).
- **El SDK mínimo de `global.json` es laxo a propósito**: el repositorio exige solo un SDK 10 (`10.0.100` o posterior) y no obliga a nadie a tener la última versión. Mantener al día el SDK de una máquina concreta es decisión de su responsable. El `Dockerfile` y la CI sí usan la última imagen estable de la serie 10.0.
- Las versiones de paquetes se centralizan en `Directory.Packages.props` (sección 3). Cuando exista la CI, las actualizaciones se automatizan con Dependabot y se revisan por PR.
