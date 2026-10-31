# Agentic AI Sandbox · .NET 10 + DeepSeek

Laboratorio de arquitectura para validar, mediante una prueba de concepto (PoC), la integración de **IA agéntica en procesos transaccionales empresariales** (estilo ERP/CRM).

El motor de razonamiento y de invocación de herramientas (*Function Calling*) es la **API de DeepSeek**, consumida a través de [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) (`IChatClient`).

> **Esto no es un ERP.** No se integra ningún sistema real. La lógica de negocio vive en servicios de dominio *in-memory* (dummy) que simulan validaciones, fallos inducidos y cambios de estado, para poder estudiar cómo se comporta un agente frente a ellos.

## Objetivos del laboratorio

- Validar el patrón *LLM como orquestador, dominio como fuente de verdad*.
- Comprobar que las herramientas (`AIFunction`) exponen un contrato estable y seguro al modelo.
- Diseñar y probar **guardrails** (validación, límites, confirmación humana, auditoría) como middleware del pipeline.
- Observar el comportamiento del agente ante errores deterministas, datos inválidos e intentos de acción no permitida.
- Obtener casos de prueba reproducibles, incluso sin llamar a DeepSeek (cliente simulado).

## Arquitectura

```mermaid
flowchart LR
    U[Usuario / Escenario] --> R[Agent Runner]

    subgraph Pipeline["IChatClient pipeline (Microsoft.Extensions.AI)"]
        direction TB
        G1[Guardrails de entrada<br/>validación, límites, PII]
        FI[Function Invocation<br/>bucle de tool-calling]
        G2[Guardrails de herramientas<br/>autorización, confirmación, auditoría]
        G1 --> FI --> G2
    end

    R --> G1
    FI <-->|chat + tools| DS[(API DeepSeek<br/>OpenAI-compatible)]
    G2 --> T[Tools / AIFunction]

    subgraph Domain["Dominio dummy in-memory"]
        direction TB
        S1[OrderService]
        S2[CustomerService]
        S3[InventoryService]
        FX[Inyector de fallos]
    end

    T --> S1 & S2 & S3
    FX -.-> S1 & S2 & S3
    R --> O[Salida + trazas + auditoría]
```

Versión ASCII:

```
 Usuario/Escenario
        │
        v
  ┌───────────┐     ┌─────────────────────────────────────────────┐
  │  Agent    │────>│ IChatClient pipeline                        │
  │  Runner   │     │ [Guardrails entrada] -> [FunctionInvocation]│<──> API DeepSeek
  └───────────┘     │            └─> [Guardrails de tools]        │
        ^           └──────────────────────┬──────────────────────┘
        │                                  v
   Salida/Trazas                    Tools (AIFunction)
                                           v
                          Servicios de dominio dummy (in-memory)
                          Orders · Customers · Inventory · Fallos
```

### Principios clave

- El **LLM nunca toca el dominio directamente**: solo a través de tools finas que delegan en servicios.
- Las **reglas de negocio** viven en el dominio, nunca en prompts.
- Los errores de negocio se devuelven al modelo como **resultados tipados**, no como excepciones.

## Requisitos previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Una **API key de DeepSeek** ([platform.deepseek.com](https://platform.deepseek.com))
- [Node.js](https://nodejs.org) (LTS) y [OpenSpec](https://github.com/Fission-AI/OpenSpec) para el flujo de trabajo guiado por especificaciones:

  ```bash
  npm install -g @fission-ai/openspec@latest
  openspec --version
  ```
- (Opcional) IDE con soporte para .NET 10: Visual Studio 2026, Rider o VS Code + C# Dev Kit

## Guía rápida

```bash
# 1. Clonar
git clone <url-del-repo> agentic-ai-sandbox
cd agentic-ai-sandbox

# 2. Configurar la API key (User Secrets, nunca en el repo)
dotnet user-secrets init --project src/Sandbox.Runner
dotnet user-secrets set "DeepSeek:ApiKey" "<tu-api-key>" --project src/Sandbox.Runner

# 3. Compilar y probar
dotnet build
dotnet test

# 4. Ejecutar un escenario
dotnet run --project src/Sandbox.Runner -- --scenario create-order
```

Alternativa por variable de entorno: `DEEPSEEK__APIKEY=<tu-api-key>`.

Configuración por defecto (`appsettings.json`):

```json
{
  "DeepSeek": {
    "Endpoint": "https://api.deepseek.com",
    "Model": "deepseek-chat"
  }
}
```

> Los endpoints y modelos disponibles pueden cambiar; consulta la documentación oficial de DeepSeek. Verifica el soporte de *function calling* del modelo elegido antes de usarlo en un escenario.

## Documentación de aprendizaje

Este laboratorio sirve también para **aprender** los conceptos que valida. La carpeta [`docs/learn/`](docs/learn/README.md) es el cuaderno de estudio y crece en paralelo al código:

- [IA agéntica en procesos transaccionales](docs/learn/01-ia-agentica-en-sistemas-transaccionales.md): cómo adaptar un ERP/CRM y qué enfoques existen.
- [Motor de razonamiento e invocación de herramientas](docs/learn/02-motor-de-razonamiento-y-herramientas.md)
- [Function Calling](docs/learn/03-function-calling.md)
- [Herramientas de Microsoft](docs/learn/04-herramientas-microsoft.md), con tabla de referencia.
- [Guardrails](docs/learn/05-guardrails.md) y patrones de implementación.
- [Autenticación y autorización de agentes](docs/learn/06-autenticacion-y-autorizacion.md) con datos sensibles.
- [Glosario](docs/learn/glosario.md)

## Flujo de trabajo con OpenSpec

El desarrollo sigue *spec-driven development* con [OpenSpec](https://github.com/Fission-AI/OpenSpec): ningún cambio de comportamiento se implementa sin una propuesta y unas especificaciones previas.

1. **Proponer**: crear un *change* (en Claude Code, `/opsx:propose "idea"`; por CLI, `openspec new change <nombre>`) en `openspec/changes/<nombre>/` con `proposal.md`, `tasks.md` y los *spec deltas* (requisitos y escenarios).
2. **Validar**: `openspec validate <nombre> --strict`.
3. **Implementar** (`/opsx:apply`): ejecutar las tareas de `tasks.md` y marcarlas al completarlas.
4. **Archivar**: `openspec archive <nombre>` fusiona los deltas en `openspec/specs/`, que pasa a ser la fuente de verdad del comportamiento actual.

Comandos útiles: `openspec list`, `openspec list --specs`, `openspec show <item>`.

Cada fase de [`docs/plan.md`](docs/plan.md) se materializa en uno o varios *changes*. Los comandos exactos pueden variar según la versión instalada; consulta `openspec --help`.

## Flujo de Git

`main` está protegida. Cada cambio se desarrolla en su propia rama (`<tipo>/<descripcion>`, por ejemplo `feat/add-order-service`) y se integra mediante *pull request* con *squash merge*. Un change de OpenSpec equivale a una rama. Detalle en [`AGENTS.md`](AGENTS.md), sección 9.

## Estructura prevista del proyecto

```
agentic-ai-sandbox/
├── AGENTS.md                      # Guía para asistentes de IA
├── README.md
├── Directory.Build.props          # Nullable, LangVersion, analizadores
├── Directory.Packages.props       # Central Package Management
├── global.json                    # Fija el SDK de .NET 10
├── AgenticSandbox.slnx
├── openspec/
│   ├── config.yaml                # Esquema y contexto del proyecto para OpenSpec
│   ├── specs/                     # Especificaciones vigentes (fuente de verdad)
│   └── changes/                   # Propuestas en curso (+ archive/)
├── docs/
│   ├── plan.md                    # Plan de trabajo por fases
│   ├── learn/                     # Documentación de aprendizaje (en paralelo al código)
│   └── adr/                       # Decisiones de arquitectura
├── src/
│   ├── Sandbox.Domain/            # Entidades, records, reglas de negocio
│   ├── Sandbox.Application/       # Commands/Queries, servicios dummy in-memory
│   ├── Sandbox.Agents/            # Tools (AIFunction), pipeline, guardrails, runner
│   └── Sandbox.Runner/            # Host de consola: DI, config, escenarios
└── tests/
    ├── Sandbox.Domain.Tests/
    ├── Sandbox.Agents.Tests/      # Tools y guardrails con IChatClient falso
    └── Sandbox.Scenarios.Tests/   # Escenarios end-to-end deterministas
```

## Estado

Fase actual: **0 · Bootstrap y documentación**. Consulta [`docs/plan.md`](docs/plan.md) y [`AGENTS.md`](AGENTS.md).

## Aviso

Proyecto experimental sin garantías. No usar en producción ni con datos reales.
