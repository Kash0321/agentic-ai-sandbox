# 7. Contenedores en sistemas agénticos

> **Estado**: documento base, escrito **antes** de implementar (fase 1). Las mediciones de velocidad están por rellenar con datos reales. **Verificado**: 2026-10-31 contra Microsoft Learn lo que se cita con enlace; lo demás se marca **(no verificado)**.

## Por qué contenerizar este proyecto

| Motivo | Qué aporta aquí |
|--------|-----------------|
| **Reproducibilidad** | Cualquiera construye y ejecuta lo mismo sin instalar SDK ni dependencias a mano |
| **Paridad con la CI** | La CI construye el mismo `Dockerfile`: "en mi máquina funciona" deja de ser un argumento |
| **Aislamiento** | La app corre con un sistema de ficheros, usuario y red acotados |
| **Límite de impacto** | Si algo falla o es manipulado, el daño queda dentro del contenedor |
| **Aprendizaje** | Es una competencia útil y se practica en un caso sencillo y controlado |

Es un caso fácil de contenerizar: una app de consola, sin base de datos, con una única dependencia externa (la API de DeepSeek). Los puntos delicados son tres: **la API key**, que **el repositorio es público** y que **la consola es interactiva**.

## Conceptos base

- **Imagen**: plantilla inmutable con el sistema de ficheros y la configuración de arranque. Se compone de **capas**.
- **Contenedor**: ejecución de una imagen, con su propio proceso, red y sistema de ficheros.
- **`Dockerfile`**: receta para construir una imagen. Cada instrucción genera una capa.
- **Build multi-etapa**: varias etapas en un mismo `Dockerfile`; solo la última forma la imagen final. Permite compilar con el SDK (pesado) y ejecutar con el runtime (ligero). Microsoft lo recomienda para reducir el tamaño de la imagen.
- **Caché de capas**: si una capa y todo lo anterior no cambian, se reutiliza. Por eso se copian primero los ficheros que definen dependencias (`*.csproj`, `Directory.Packages.props`) y se restauran **antes** de copiar el código.
- **Digest**: identificador inmutable de una imagen (`@sha256:...`). Fijar la imagen base por tag y, mejor, por digest evita que el contenido cambie sin avisar. Microsoft lo recomienda como buena práctica.
- **Compose**: define servicios, redes, volúmenes y secretos de una aplicación multicontenedor en un fichero.
- **Bind mount y volumen**: forma de compartir ficheros con el contenedor. El primero apunta a una carpeta del anfitrión; el segundo lo gestiona el motor de contenedores.

## Cómo se contenerizan las apps .NET

Dos vías, ambas oficiales:

| Vía | Cómo | Cuándo |
|-----|------|--------|
| **`Dockerfile`** | `docker build` con un fichero propio | Máximo control; permite una etapa `test`; enseña cada pieza |
| **Contenedores del SDK** | `dotnet publish /t:PublishContainer`, **sin Dockerfile** | Casos simples; el SDK genera la imagen |

Datos verificados sobre la vía del SDK:

- Está incluida desde el SDK 8.0.200 y no necesita paquetes adicionales. En aplicaciones de consola puede hacer falta activar `EnableSdkContainerSupport`.
- **No necesita Docker para construir** la imagen; Docker o Podman solo hacen falta para ejecutarla en local.
- La imagen base se infiere del proyecto (`mcr.microsoft.com/dotnet/runtime` para una consola) y se puede cambiar con `ContainerBaseImage` o `ContainerFamily`.
- Con .NET 8 o superior y las imágenes de Microsoft, el contenedor corre por defecto con el usuario **sin privilegios** `app`.

Las imágenes *chiseled* de Ubuntu llevan solo lo imprescindible para .NET: **no tienen shell ni gestor de paquetes**, tienen menos CVE y ya vienen con usuario no root.

**Decisión del proyecto (criterio propio)**: usar `Dockerfile` explícito, porque el objetivo es aprender y necesitamos la etapa `test`. La vía del SDK queda como comparación posible.

## Diseño previsto

### Etapas del `Dockerfile`

```
 sdk (fijada) > restore > build > test > publish > final
                (cache)            ^                (runtime, no root)
                                   docker build --target test
```

- `restore`: copia solo los ficheros de dependencias y restaura. Es la capa más cacheable.
- `test`: ejecuta los tests **sin red** (usan un `IChatClient` falso). Es lo que ejecuta la CI.
- `final`: imagen de runtime, usuario no root, punto de entrada de la app.

### Compose

- `runner`: servicio *one-shot* para ejecutar escenarios: `docker compose run --rm runner --scenario create-order`.
- `dashboard` (perfil opcional): contenedor del **Aspire Dashboard** autónomo, que recibe telemetría OTLP. Microsoft aclara que es una herramienta de desarrollo, no de monitorización en producción, y que por defecto exige un token de acceso.

### Secretos

El repositorio es público, así que:

- La API key **nunca** va en la imagen, el `Dockerfile` (`ARG`/`ENV`), `compose` ni un `.env` versionado. Solo se versiona `.env.example`.
- Opción recomendada: secreto de compose basado en **fichero** (`secrets/deepseek_api_key`, ignorado por Git), montado en `/run/secrets/` y leído con un proveedor de configuración por ficheros. A diferencia de una variable de entorno, no aparece en `docker inspect`. **(No verificado)** en esta sesión: la ruta de montaje y el proveedor concreto.
- Alternativa simple: la variable de entorno `DeepSeek__ApiKey`.

### Endurecimiento (a validar en la práctica)

Usuario no root, sistema de ficheros raíz de solo lectura, `cap_drop: ALL`, `no-new-privileges`, límites de memoria y CPU, `.dockerignore` estricto, imágenes fijadas y escaneo de la imagen en CI. **(No verificado)** el conjunto exacto de opciones y la herramienta de escaneo: se contrastarán al implementarlo.

### CI

Un workflow en cada PR con dos tareas: `docker build --target test` y construcción de la imagen final con escaneo. Permisos mínimos y acciones fijadas por SHA. Cuando exista, se añade como check obligatorio al ruleset de `main`.

## Velocidad: ¿merece la pena "solo contenedor"?

Hay dos formas de entender "solo contenedor", con costes muy distintos:

| Modo | Coste | Comentario |
|------|-------|-----------|
| **Reconstruir la imagen en cada ciclo** | Alto y evitable | La caché de capas acelera el restore, pero la compilación se repite salvo que se configuren cachés de build |
| **Dev container** (desarrollar dentro de un contenedor persistente) | Bajo, con coste de montaje | Se parece más al uso nativo; exige configuración |

Factores a tener en cuenta:

- **CPU**: en contenedores Linux sobre WSL2 es cercana a la nativa **(no verificado)**.
- **I/O de ficheros**: es el coste real. Con el código en una carpeta de Windows montada en el contenedor, restaurar y compilar muchos ficheros pequeños puede ser mucho más lento. Se evita guardando el repo en el sistema de ficheros de WSL o usando volúmenes con nombre.
- **Herramientas**: depurador, IDE, `openspec`, `gh` y el asistente dentro o fuera del contenedor.
- **Costes fijos**: habilitar WSL2, instalar el motor (reinicio y permisos de administrador), RAM de la máquina virtual e imágenes que descargar.
- **Beneficio**: reproducibilidad. En un proyecto en solitario con un solo SDK, la CI sobre el mismo `Dockerfile` ya la proporciona casi toda.

**Decisión del proyecto**: flujo **híbrido**. `dotnet` local para iterar rápido y el contenedor como referencia para dar algo por terminado y para la CI. El flujo "solo contenedor" es un **experimento acotado en la fase 1**.

### Experimento (fase 1): resultados por rellenar

Medir en el equipo de desarrollo, tres repeticiones cada una:

| Escenario | En frío | En caliente | Observaciones |
|-----------|---------|-------------|---------------|
| `dotnet test` nativo | _por medir_ | _por medir_ | |
| `docker build --target test` | _por medir_ | _por medir_ | |
| Dev container (opcional) | _por medir_ | _por medir_ | |

Criterio de decisión (propio): si el ciclo de tests en contenedor tarda menos del doble que el nativo y no estorba, merece mantenerlo como flujo por defecto. Si no, queda solo como referencia y para la CI. Se anotarán aquí los resultados y la conclusión.

## Contenedor y seguridad agéntica

- Un contenedor **no sustituye a los guardrails** (documento 5) ni a la autorización (documento 6). Limita el daño a nivel de sistema; no impide que el agente haga algo permitido pero equivocado dentro de la app.
- **Control de salida de red**: la app solo necesita llegar a la API del modelo. Restringir el tráfico saliente a destinos concretos es una medida de segmentación de red que Microsoft recomienda para funciones de agentes. En compose esto no es trivial (no se filtra por dominio sin un proxy); queda como ejercicio posterior.
- **Sandbox de herramientas**: en este proyecto las tools son *in-memory* y no ejecutan código. Pero si un agente ejecutara comandos o código generado, el contenedor (mejor, uno efímero y sin red) sería la barrera básica. Es un patrón a conocer.
- **Datos**: no se montan datos reales en el contenedor. En el sandbox solo hay datos sintéticos.

## Errores típicos

- Secretos en `ARG`, `ENV` o capas de la imagen: quedan en el historial de la imagen aunque se borren después.
- Imagen base `latest` o sin fijar: construcciones no reproducibles.
- Ejecutar como root.
- Copiar todo el código antes del `restore`: se pierde la caché.
- Olvidar el `.dockerignore`: se cuelan `bin/`, `obj/`, `.git` o, peor, un `.env`.
- Saltos de línea CRLF en scripts o `Dockerfile`: fallan en Linux (`/bin/sh^M`). Por eso la fase 1 incluye `.gitattributes` con `eol=lf`.
- Montar el código desde una carpeta de Windows y quejarse de la lentitud.
- Creer que el contenedor es una frontera de seguridad completa.

## Referencias

- Contenedores con el SDK de .NET (sin Dockerfile): <https://learn.microsoft.com/dotnet/core/containers/sdk-publish>
- Visión general y propiedades necesarias: <https://learn.microsoft.com/dotnet/core/containers/overview>
- Referencia de configuración (imagen base, usuario): <https://learn.microsoft.com/dotnet/core/containers/publish-configuration>
- Tutorial de Dockerfile para .NET (multi-etapa, digest): <https://learn.microsoft.com/dotnet/core/docker/build-container>
- Introducción a .NET y Docker (imágenes, *chiseled*): <https://learn.microsoft.com/dotnet/core/docker/introduction>
- Aspire Dashboard autónomo: <https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example>
- Dev containers con .NET: <https://learn.microsoft.com/dotnet/architecture/microservices/net-core-net-framework-containers/official-net-docker-images>
- Condiciones de licencia de Docker Desktop, WSL2 y herramientas de escaneo: **no verificado**; comprobar la política de tu organización antes de instalar.
