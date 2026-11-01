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
| **CRLF / LF** | Formas de terminar una línea: `\r\n` (Windows) o `\n` (Linux y macOS) |
| **`.gitattributes`** | Fichero versionado que asigna atributos de Git por ruta; aquí, cómo se convierten los saltos de línea |
| **`text=auto`** | Atributo de Git: si el fichero es texto, se guarda con LF en el índice (salvo que ya estuviera con CRLF) |
| **`eol`** | Atributo de Git que fija el salto de línea en el árbol de trabajo al extraer (`lf` o `crlf`); activa `text` |
| **`core.autocrlf`** | Configuración local de Git que convierte saltos de línea; no viaja con el repositorio y los atributos la sustituyen |
| **BuildKit** | Motor de construcción de imágenes de Docker; ejecuta las etapas, gestiona la caché y admite opciones como `RUN --network` |
| **Contexto de build** | Conjunto de ficheros que el cliente envía al motor para construir; lo filtra el `.dockerignore` |
| **`.dockerignore`** | Fichero que excluye rutas del contexto de build; aquí, de lista blanca (se excluye todo y se reincluye lo necesario) |
| **`RUN --network=none`** | Opción de una instrucción `RUN` que ejecuta ese paso sin red; aquí, la etapa de tests |
| **Lista de manifiestos (índice multiplataforma)** | Entrada del registro que agrupa las variantes de una imagen por arquitectura; su digest es el que se fija |
| **Imagen colgante (*dangling*)** | Imagen sin repositorio ni etiqueta y sin contenedor que la use; la borra `docker image prune` |
| **Forma *exec* (`ENTRYPOINT`)** | `ENTRYPOINT` escrito como lista JSON: el proceso arranca sin shell intermedio y recibe las señales directamente |
| **ICU** | International Components for Unicode: biblioteca de datos de cultura (formatos, ordenación, mayúsculas) que usa .NET |
| **Globalización invariante (modo)** | Modo de .NET sin datos de cultura: todo se comporta como la cultura invariante y no hace falta ICU |
| **`PredefinedCulturesOnly`** | Opción de .NET que, en modo invariante, impide crear culturas distintas de la invariante (lanza `CultureNotFoundException`) |
| **ADR** | Architecture Decision Record: documento breve que registra una decisión de arquitectura, su contexto, sus alternativas y sus consecuencias |
| **Renormalizar** | Volver a aplicar los atributos de saltos de línea a lo ya versionado (`git add --renormalize .`) |
| **CI (integración continua)** | Ejecución automática de las comprobaciones (build, tests, validaciones) en cada cambio propuesto o integrado |
| **GitHub Actions** | Servicio de CI de GitHub: ejecuta *workflows* definidos en `.github/workflows/` en máquinas llamadas *runners* |
| **Workflow / job / step** | Fichero YAML con disparadores (workflow), unidades que se ejecutan en un runner (jobs) y los pasos de cada una (steps) |
| **Runner** | Máquina (aquí, una VM `ubuntu-24.04` de GitHub) que ejecuta un job y se descarta al terminar |
| **`GITHUB_TOKEN`** | Token que GitHub da a cada job; sus permisos se declaran con `permissions:` y deben ser los mínimos |
| **`pull_request_target`** | Evento que ejecuta el workflow con el contexto y los permisos de la rama base; peligroso si ejecuta código del PR |
| **Fijación por SHA (*SHA pinning*)** | Referenciar una acción por el SHA completo de un commit en vez de por etiqueta, que se puede mover |
| **Ataque de reetiquetado** | Mover las etiquetas de versión de una acción o paquete a código malicioso para que lo ejecute quien las usa |
| **Check obligatorio (*required status check*)** | Check que el ruleset exige en verde para fusionar; se identifica por su nombre (`context`) y la app que lo publica (`integration_id`) |
| **Dependabot** | Servicio de GitHub que propone por PR actualizaciones de versión y de seguridad de las dependencias, y emite alertas |
| **Periodo de espera (*cooldown*)** | Días que Dependabot espera tras publicarse una versión antes de proponerla; por defecto 3 en las de versión |
| **CVE** | Identificador público de una vulnerabilidad conocida |
| **Grype** | Escáner de vulnerabilidades de Anchore: lista los paquetes de una imagen y los cruza con una base de datos de avisos |
| **`only-fixed`** | Opción del escáner que solo considera las vulnerabilidades con versión corregida publicada |
| **Caché `gha` (BuildKit)** | Caché de capas de BuildKit guardada en la caché de GitHub Actions; separada por `scope` |
| **`workflow_dispatch`** | Disparador para lanzar un workflow a mano; solo funciona si el workflow está en la rama por defecto |
| **Telemetría** | Envío de estadísticas de uso por parte de una herramienta; la CLI de OpenSpec la tiene y se desactiva con `OPENSPEC_TELEMETRY=0` |
| **Host genérico** | `Microsoft.Extensions.Hosting`: arranque estándar de una app .NET con inyección de dependencias, configuración y registro; `Host.CreateApplicationBuilder` lo prepara con valores por defecto |
| **Raíz de contenido (*content root*)** | Carpeta desde la que el host busca ficheros como `appsettings.json`; por defecto, el directorio actual |
| **Proveedor de configuración** | Fuente de pares clave-valor (fichero JSON, variables de entorno, línea de comandos, ficheros de secretos…); el último añadido sobrescribe a los anteriores |
| **Entorno (`DOTNET_ENVIRONMENT`)** | Nombre del entorno de ejecución (`Development`, `Production`…); si no se define, es `Production` |
| **User Secrets** | Fichero JSON en el perfil del usuario, fuera del repositorio, para credenciales de desarrollo; el host solo lo carga en `Development` y no está cifrado |
| **`launchSettings.json`** | Perfiles de lanzamiento de `dotnet run` y de los IDE (variables de entorno, argumentos); no llega a la publicación |
| ***Key-per-file*** | Proveedor de configuración que convierte cada fichero de un directorio en una clave; encaja con los secretos de Docker en `/run/secrets` |
| **Secreto de compose** | Fichero que compose monta dentro del contenedor (por defecto en `/run/secrets/<nombre>`); no aparece en las variables de entorno ni en `docker inspect` |
| **Patrón de opciones** | Agrupar una sección de configuración en una clase tipada (`IOptions<T>`) con validación propia |
| **`ValidateOnStart`** | Ejecuta la validación de unas opciones al arrancar el host en lugar de cuando se leen por primera vez |
| **Código de salida** | Número que devuelve un proceso al terminar; `0` es éxito y el resto lo define cada programa (aquí, `ExitCodes`) |
| **Salida estándar / de errores (`stdout` / `stderr`)** | Los dos flujos de salida de una consola; aquí el resultado va por el primero y los registros y errores por el segundo |
| **Razonamiento (*thinking*)** | Fase en la que un modelo "piensa" antes de responder; cuesta tokens de salida. En DeepSeek está activado por defecto y aquí se desactiva |
| **`TextReasoningContent`** | Contenido de `Microsoft.Extensions.AI` que lleva el razonamiento del modelo, separado del `TextContent` de la respuesta |
| **`RawRepresentationFactory`** | Propiedad de `ChatOptions` que devuelve las opciones propias del proveedor, para ajustes que `ChatOptions` no expresa |
| **`JsonPatch` (`SCME0001`)** | API experimental de `System.ClientModel` que añade campos arbitrarios al JSON de una petición; aquí, `thinking` |
| **Remapeo de modelo** | El proveedor atiende una petición con un modelo distinto del pedido; en DeepSeek, `deepseek-chat` responde como `deepseek-flash` |
| **Caché de *prompt*** | El proveedor reutiliza el prefijo repetido de las peticiones y lo cobra más barato; DeepSeek la aplica sin pedirla |
| **Transporte HTTP simulado** | `HttpMessageHandler` de pruebas que devuelve respuestas grabadas; permite probar el cliente real sin red |
