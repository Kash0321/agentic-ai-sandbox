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
| **Aspire Dashboard** | Panel de desarrollo que recibe telemetría OTLP (trazas, métricas, logs); existe como contenedor autónomo |
| **Bind mount** | Carpeta del anfitrión compartida con un contenedor; en Windows puede ser lenta con muchos ficheros pequeños |
| **Capa (de imagen)** | Resultado de una instrucción del `Dockerfile`; se cachea y reutiliza si no cambia |
| **Chiseled** | Imagen mínima de Ubuntu para .NET, sin shell ni gestor de paquetes y con usuario no root |
| **Compose** | Fichero que define servicios, redes, volúmenes y secretos de una aplicación multicontenedor |
| **Contenedor** | Ejecución aislada de una imagen, con su propio proceso, red y sistema de ficheros |
| **Dev container** | Entorno de desarrollo completo definido como contenedor (especificación abierta, usada por VS Code y Codespaces) |
| **Digest** | Identificador inmutable (`sha256`) de una imagen; permite fijar exactamente qué se usa |
| **Dockerfile** | Receta para construir una imagen de contenedor |
| **Imagen (de contenedor)** | Plantilla inmutable, formada por capas, a partir de la que se crea un contenedor |
| **Multi-etapa (build)** | Varias etapas en un `Dockerfile`; solo la última forma la imagen final |
| **OTLP** | OpenTelemetry Protocol, formato estándar para enviar trazas, métricas y logs |
| **Rootless / no root** | Contenedor que corre con un usuario sin privilegios |
| **Volumen** | Almacenamiento que gestiona el motor de contenedores, independiente del sistema de ficheros del anfitrión |
| **WSL2** | Subsistema de Windows para Linux; base habitual de Docker Desktop en Windows |
| **`.gitignore`** | Fichero con patrones de lo que Git no debe ofrecer para versionar; no protege lo que ya está versionado |
| **`global.json`** | Fichero que indica qué SDK de .NET usa un repositorio y qué versiones acepta |
| **Framework de destino (TFM)** | Versión de .NET para la que se compila una aplicación, por ejemplo `net10.0` |
| **Runtime de .NET** | Componente que ejecuta una aplicación .NET ya compilada |
| **`rollForward`** | Política de `global.json` que decide qué SDK usar cuando no está la versión exacta pedida |
| **SDK de .NET** | Herramientas para compilar y probar (compilador, CLI, MSBuild); distinto del runtime y del framework de destino |
| **`AnalysisLevel`** | Propiedad de MSBuild que fija con qué conjunto de reglas de análisis de código se trabaja (`<versión>-<modo>`); evita que un SDK nuevo añada reglas |
| **Analizador (de código)** | Componente que inspecciona el código al compilar y emite diagnósticos: `CS` (compilador), `CA` (calidad) o `IDE` (estilo) |
| **Central Package Management (CPM)** | Forma de declarar las versiones de los paquetes NuGet en un único fichero, `Directory.Packages.props`, sin versión en los `.csproj` |
| **`Directory.Build.props`** | Fichero de MSBuild que aplica propiedades comunes a todos los proyectos de la carpeta y sus subcarpetas |
| **Inversión de dependencias (capas)** | Organización en la que las dependencias apuntan hacia el dominio, de modo que este no conoce infraestructura como el LLM |
| **Package source mapping** | Configuración de NuGet que indica de qué fuente se restaura cada paquete; da reproducibilidad y evita ciertos ataques de sustitución |
| **Raíz de composición** | Único punto de una aplicación que conoce todas las capas y las ensambla (aquí, `Sandbox.Runner`) |
| **`.slnx`** | Formato de solución en XML sencillo; es el que genera por defecto `dotnet new sln` desde .NET 10 |
| **Microsoft.Testing.Platform (MTP)** | Ejecutor de tests moderno de .NET, alternativa al clásico VSTest; en el SDK 10 se habilita con `test.runner` en `global.json` |
| **Test de arquitectura** | Test que comprueba reglas de estructura (por ejemplo, qué capa puede depender de cuál) en vez de comportamiento |
| **Test de cableado** | Test mínimo que solo comprueba que el ensamblado bajo prueba está bien referenciado y se carga; marcador hasta que haya tests reales |
| **xUnit v3** | Versión actual del marco de pruebas xUnit; sus proyectos de tests son ejecutables y se ejecutan con Microsoft.Testing.Platform |
