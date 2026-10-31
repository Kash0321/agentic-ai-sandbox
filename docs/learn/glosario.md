# Glosario

Vocabulario del proyecto. Se amplía a medida que aparecen términos nuevos.

| Término | Significado |
|---------|-------------|
| **Agente** | Sistema que persigue un objetivo usando un LLM para razonar, herramientas para actuar y contexto para decidir |
| **Agentic workflow** | Flujo de pasos en el que intervienen uno o varios agentes |
| **ABAC / RBAC / ReBAC** | Autorización por atributos / por roles / por relaciones |
| **AIFunction** | Tipo de `Microsoft.Extensions.AI` que representa una función descrita al modelo e invocable |
| **Allow-list** | Lista de lo explícitamente permitido; lo demás se deniega |
| **Auditoría** | Registro estructurado y fiable de quién hizo qué, cuándo y con qué resultado |
| **Bucle agéntico** | Ciclo en el que el modelo pide una tool, se ejecuta y se le devuelve el resultado, hasta que responde |
| **ChatClientBuilder** | Constructor del pipeline de `IChatClient` por capas |
| **Contexto (ventana de)** | Cantidad máxima de texto/tokens que el modelo puede procesar en una llamada |
| **CQRS** | Separación entre operaciones que cambian estado (commands) y que solo leen (queries) |
| **DelegatingChatClient** | Clase base para crear una capa de middleware en el pipeline de `IChatClient` |
| **Dry-run** | Ejecución simulada que muestra el efecto sin aplicarlo |
| **Embedding** | Representación numérica de un texto para comparar significado |
| **Fail closed** | Ante error o duda, denegar |
| **Function calling / tool calling** | Capacidad del modelo de pedir la ejecución estructurada de una función |
| **Guardrail** | Control determinista (o complementario probabilístico) que limita entradas, decisiones o acciones de un sistema de IA |
| **Alucinación** | Salida plausible pero falsa o inventada, incluidos argumentos de tools |
| **HITL (human-in-the-loop)** | Un humano aprueba o decide en puntos concretos del proceso |
| **IChatClient** | Abstracción de `Microsoft.Extensions.AI` para conversar con un modelo |
| **Idempotencia** | Repetir una operación produce el mismo efecto que hacerla una vez |
| **Intersección de permisos** | Permiso efectivo de un agente que actúa por un usuario: solo lo que ambos tienen |
| **JSON Schema** | Estándar para describir la estructura de un JSON; así se describen los parámetros de una tool |
| **LLM** | Modelo de lenguaje de gran tamaño |
| **MCP** | Model Context Protocol, estándar abierto para conectar apps de IA con herramientas y datos |
| **Mínimo privilegio** | Dar solo los permisos imprescindibles para la tarea |
| **OBO (On-Behalf-Of)** | Flujo OAuth 2.0 por el que un servicio actúa en nombre del usuario con sus permisos delegados |
| **OpenSpec** | Herramienta de desarrollo guiado por especificaciones usada en este proyecto |
| **PDP / PEP** | Punto de decisión de política / punto de aplicación de política |
| **PII** | Datos de carácter personal identificables |
| **Prompt injection** | Ataque que inserta instrucciones maliciosas en texto que el modelo procesa; directa (del usuario) o indirecta (de datos externos) |
| **RAG** | Generación aumentada con recuperación: se buscan datos y se añaden al contexto |
| **ReAct** | Patrón en el que el modelo alterna razonar y actuar con herramientas |
| **Saga** | Secuencia de transacciones con acciones de compensación para deshacer si algo falla |
| **Seudonimización** | Sustituir un dato real por un identificador opaco reversible solo por el sistema |
| **Step-up** | Exigir autenticación reforzada para una acción sensible |
| **Sistema transaccional** | Sistema que ejecuta operaciones de negocio con garantías de consistencia (ERP, CRM) |
| **Token (de acceso)** | Credencial de corta vida con alcance limitado para acceder a un recurso |
| **Token (de modelo)** | Unidad de texto que procesa y factura un LLM |
| **Tool** | Herramienta que el agente puede invocar; en .NET, una `AIFunction` |
| **ToolResult / ToolError** | Resultado tipado de una tool y su error determinista, en lugar de excepciones |
| **Trifecta letal** | Combinación peligrosa: datos privados, contenido no confiable y canal de comunicación hacia fuera |
