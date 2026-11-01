# Documentación de aprendizaje

Este laboratorio tiene dos objetivos: **validar patrones** de IA agéntica en procesos transaccionales y **aprender** a fondo los conceptos que hay detrás. Esta carpeta es el cuaderno de estudio del proyecto. Se escribe **en paralelo al código**, no después.

## Cómo leerla

Orden recomendado para empezar:

| # | Documento | Pregunta que responde |
|---|-----------|-----------------------|
| 1 | [IA agéntica en procesos transaccionales](01-ia-agentica-en-sistemas-transaccionales.md) | ¿Cómo se adapta un sistema ERP/CRM para que lo use un agente? ¿Qué enfoques hay? |
| 2 | [Motor de razonamiento e invocación de herramientas](02-motor-de-razonamiento-y-herramientas.md) | ¿Qué hace exactamente el LLM dentro del bucle agéntico? |
| 3 | [Function Calling](03-function-calling.md) | ¿Cómo es el protocolo por dentro, mensaje a mensaje? |
| 4 | [Herramientas de Microsoft](04-herramientas-microsoft.md) | ¿Qué problema resuelve cada librería y cuál usamos? Incluye referencia. |
| 5 | [Guardrails](05-guardrails.md) | ¿Qué son y qué patrones existen para implementarlos? |
| 6 | [Autenticación y autorización de agentes](06-autenticacion-y-autorizacion.md) | ¿Cómo se hace de forma segura con datos sensibles? |
| 7 | [Contenedores en sistemas agénticos](07-contenedores-en-sistemas-agenticos.md) | ¿Cómo y por qué se contenerizan el build, los tests y la ejecución? ¿Cuánto cuesta? |
| 8 | [Estructura y estándares de la solución .NET](08-estructura-y-estandares-de-la-solucion-dotnet.md) | ¿Cómo se estructura la solución, qué se ignora en Git, cómo se fija el SDK, cómo se prueban las reglas de capas, cómo se normalizan los saltos de línea y por qué la globalización es invariante? |
| 9 | [Integración continua y cadena de suministro](09-integracion-continua-y-cadena-de-suministro.md) | ¿Qué comprueba la CI y con qué garantías? ¿Por qué se fijan las acciones por SHA, qué hace Dependabot y cuándo bloquea una vulnerabilidad? |
| - | [Glosario](glosario.md) | Vocabulario del proyecto. |

## Convenciones de estos documentos

- **Idioma**: español. Identificadores de código, nombres de tools y términos técnicos consolidados, en inglés.
- **Estructura**: qué es, por qué importa, cómo funciona, alternativas y trade-offs, cómo se aplica en este proyecto (con enlaces a specs y código cuando existan), errores típicos, referencias.
- **Rigor**: las afirmaciones sobre tecnología de terceros se contrastan con documentación oficial. Cada documento lleva una cabecera con la fecha de verificación y marca como **(no verificado)** lo que viene de memoria o de fuentes secundarias.
- **Distinción honesta**: se separa lo que dice la documentación de lo que es criterio de diseño propio del proyecto. Cuando algo es una recomendación y no un hecho, se dice.
- **Diagramas**: Mermaid cuando sea posible. En ASCII solo se usan caracteres monoespaciados seguros (`+ - | > < ^ v` y los de caja `┌ ─ ┐ │ └ ┘ ┬ ┴ ├ ┤ ┼`). Nada de `▶ ▼ ▲ →` dentro de dibujos: su ancho varía según la fuente y descuadra el diagrama.
- **Vivos**: se actualizan cuando el código o la tecnología cambian. Al final de cada fase se revisa qué se ha aprendido y se corrige lo que la práctica haya desmentido.

## Cuándo se escribe y se actualiza

Cada *change* de OpenSpec incluye una tarea de documentación de aprendizaje (ver `AGENTS.md`, sección 7). La tabla de abajo muestra qué documentos nacen o crecen en cada fase de [`docs/plan.md`](../plan.md).

| Fase | Documentación de aprendizaje |
|------|------------------------------|
| 0 | Los siete documentos base y el glosario (esta carpeta) |
| 1 | Nuevos: 08 (estructura y estándares de la solución .NET; con `add-container-build`, también saltos de línea y globalización invariante) y 09 (CI y cadena de suministro, con `add-ci-pipeline`). Actualizado 07 con el experimento de velocidad (en el equipo de desarrollo y en un runner de GitHub), el `Dockerfile` y la CI |
| 2 | Actualizar 04 con lo aprendido al conectar DeepSeek con `IChatClient` (endpoint personalizado, límites reales); actualizar 07 con la gestión real del secreto en contenedor |
| 3 | Nuevo: *Diseño de dominios aptos para agentes* (idempotencia, máquinas de estado, errores deterministas) |
| 4 | Actualizar 02 y 03 con trazas reales del bucle; nuevo: *Diseño de tools para LLMs* |
| 5 | Actualizar 05 y 06 con los guardrails implementados y el modelo de identidad simulado |
| 6 | Nuevo: *Pruebas y evaluación de sistemas agénticos* y *Prompt injection* |
| 7 | Informe de conclusiones y revisión global de los seis documentos base |

## Conceptos adicionales que merece la pena documentar

Lista viva de temas candidatos. Cuando uno se aborde, se convierte en documento y se marca aquí. El asistente propondrá nuevos temas al cierre de cada fase.

| Tema | Por qué importa | Se abordará en |
|------|-----------------|----------------|
| **Prompt injection** (directa e indirecta) y la "trifecta letal" (datos privados + contenido no confiable + canal de salida) | Es la amenaza específica de los sistemas agénticos y condiciona todo el diseño de guardrails | Fase 5-6 |
| **Idempotencia, sagas y compensación** | Un agente reintenta y se equivoca; las transacciones multi-paso necesitan deshacerse de forma controlada | Fase 3 |
| **Diseño de tools para LLMs** (granularidad, nombres, salidas compactas) | La calidad de las tools determina más el resultado que el prompt | Fase 4 |
| **Salidas estructuradas y JSON Schema** | Es la base de un contrato fiable entre LLM y código | Fase 4 |
| **Gestión de contexto y memoria** (historial, reducción, resumen, estado) | El modelo es sin estado; el coste y la calidad dependen de qué se reenvía | Fase 4 |
| **Observabilidad y trazabilidad** (OpenTelemetry, auditoría) | Sin trazas no se puede depurar ni auditar un agente | Fase 2 y 5 |
| **Evaluación y testing no determinista** (cliente falso, conjuntos dorados, LLM-as-judge) | Cómo probar algo que no es determinista | Fase 6 |
| **Coste, latencia y caché** (presupuestos de tokens, prompt caching) | Un bucle agéntico multiplica las llamadas | Fase 2-4 |
| **MCP (Model Context Protocol) frente a tools locales** | Estándar para exponer sistemas a agentes; relevante para integrar ERP reales | Posterior a la PoC |
| **Patrones multi-agente** (secuencial, concurrente, handoff, group chat) | Cuándo un solo agente deja de bastar | Posterior a la PoC |
| **Modelos de razonamiento frente a modelos de chat** | Cuándo compensa pagar más latencia por razonar | Fase 2 |
| **Cumplimiento y gobierno** (RGPD, AI Act, auditoría, residencia de datos) | Determina si un sistema agéntico puede usarse con datos reales | Fase 7 |
| **Fiabilidad** (timeouts, reintentos, fallback entre modelos, circuit breaker) | Un proveedor externo falla y el agente debe degradar con elegancia | Fase 5 |
| **Seguridad de la cadena de suministro** (auditoría de NuGet, *package source mapping*, archivos de bloqueo, Dependabot, SBOM) | El repositorio es público y depende de paquetes de terceros; con warnings como errores, una vulnerabilidad nueva puede romper el build | Fase 1: **en parte en el documento 09** (acciones por SHA, Dependabot, alertas). Pendiente: *package source mapping*, archivos de bloqueo de NuGet y SBOM |
| **Builds reproducibles** (SDK y paquetes fijados, builds deterministas, mismo resultado en local, contenedor y CI) | Es el fundamento de poder fiarse de que "en verde" significa lo mismo en todas partes | Fase 1: **hecho** (documentos 07, 08 y 09: imágenes por digest, globalización invariante, CI sobre el mismo `Dockerfile`) |
| **Escaneo de vulnerabilidades de imágenes** (CVE de la imagen base, SBOM de la imagen, cuándo romper el build) | Fijar por digest congela también los parches; hay que saber cuándo una base fijada se ha quedado vulnerable | Fase 1: **hecho** (documento 09 y ADR 0002). Pendiente: SBOM como artefacto |
| **Seguridad de GitHub Actions** (`pull_request_target`, inyección en expresiones `${{ }}`, *cache poisoning*, OIDC frente a secretos de larga duración) | La CI de un repositorio público es una superficie de ataque; en cuanto la fase 2 necesite un secreto o la CI publique algo, hará falta | Fase 2 o al publicar imágenes |
| **Procedencia y firma de artefactos** (atestaciones, SLSA, firma de imágenes) | Demostrar que una imagen sale de este repositorio y de este workflow, no solo que su contenido es el esperado | Posterior a la PoC |
| **Control de salida de red (egress) de agentes** (red `none`, redes internas de compose, proxy con lista de dominios) | El `runner` pasa de no tener red a tener que hablar con un único destino; limitar a qué puede llegar un agente es una defensa clave | Fase 2 y 5 |
