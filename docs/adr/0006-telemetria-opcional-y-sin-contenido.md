# ADR 0006: Telemetría desactivada por defecto, sin contenido y sin errores del proveedor

- **Estado**: aceptada
- **Fecha**: 2026-11-07
- **Change**: `add-agent-observability` (decisiones D2, D5, D6 y D12 de su `design.md`)

## Contexto

El tercer change de la fase 5 añade OpenTelemetry: trazas, registros y métricas del agente y del modelo, exportados por OTLP a un panel. Al registrar todo lo que pasa en una ejecución, la telemetría se convierte en una **vía de salida de datos nueva**, paralela a la salida estándar y a los registros de consola, que el proyecto ya cuidaba (spec `chat-client`, "Registro de llamadas sin secretos ni contenidos"; auditoría sin argumentos en `agent-guardrails`).

El explore del change encontró tres hechos que obligan a decidir una política, y no solo una configuración:

1. `OpenTelemetryChatClient`, de `Microsoft.Extensions.AI` 10.11.0, copia **el mensaje de la excepción** en el estado del span. El error que devuelve DeepSeek ante una clave rechazada repite sus cuatro últimos caracteres. Bastaba con colocar el cliente de telemetría en el sitio equivocado del pipeline para que el panel mostrara un fragmento de la clave.
2. La librería puede capturar el **contenido** de las conversaciones (instrucciones del sistema, petición, argumentos y resultados de las tools). Es muy útil para aprender y depurar, y es justo lo que en un sistema real no debe salir del proceso sin una decisión expresa. La variable estándar de las convenciones GenAI para activarlo (`OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`) no tiene efecto en esa versión.
3. Con el endpoint configurado y nadie escuchando, el Runner tarda entre 9 y 12 s más en salir, sin ningún aviso.

## Decisión

1. **La telemetría está desactivada por defecto.** Solo se exporta cuando la opción estándar `OTEL_EXPORTER_OTLP_ENDPOINT` tiene valor. Sin ella no se registra ningún componente de OpenTelemetry, y compose la deja vacía salvo que quien ejecuta la defina. Un valor inválido se rechaza al arrancar (código 2), y al arrancar con telemetría se registra hacia dónde se envía, sin credenciales embebidas.
2. **Sin contenido por defecto.** La captura de contenido es una opción propia del proyecto (`Telemetry:CaptureContent`), desactivada por defecto. Activada, el Runner avisa al arrancar. El contenido nunca llega a los registros de consola: el cliente de telemetría se crea sin `ILoggerFactory`.
3. **El texto de error del proveedor nunca llega a la telemetría.** El cliente de telemetría va por fuera del traductor de errores, así que solo ve excepciones del proyecto, cuyos mensajes se construyen sin el texto del proveedor. El span raíz del agente registra el tipo de una excepción, nunca su mensaje.
4. **Cualquier telemetría futura del proyecto sigue las mismas reglas**: se activa expresamente, no lleva contenido salvo opción explícita y no copia mensajes de excepción que puedan venir de fuera.

## Alternativas consideradas

- **Telemetría siempre activa con un endpoint por defecto** (por ejemplo, el del panel): es cómoda, pero sin panel cada ejecución tarda segundos de más sin explicación, y la CI exportaría a ninguna parte.
- **Capturar contenido siempre en desarrollo**: el panel sería más didáctico sin pasos extra, pero una opción que se activa sola en un entorno acaba activa en otro. Se prefirió el aviso explícito.
- **Filtrar los atributos sensibles con un procesador de OpenTelemetry**: permitiría capturar todo y limpiar después, pero depende de conocer cada atributo que la librería añade, y las convenciones GenAI son experimentales y cambian de nombre. No generar el dato es más robusto que borrarlo.
- **Registrar la excepción completa en el span raíz** (`AddException`): da más detalle al depurar, pero copia el mensaje y la traza, que pueden venir del proveedor.

## Consecuencias

- **Sin endpoint, el comportamiento es idéntico** al de antes del change: misma salida, mismo código de salida y ninguna conexión.
- **La CI no cambia**: el check `image` ejecuta el Runner sin endpoint.
- **El panel muestra la forma y los tiempos de una ejecución, no su contenido**, salvo que se pida. Para ver los mensajes hay que activar la opción, y el aviso queda en el registro.
- **El orden del pipeline del cliente de chat es parte de la seguridad**: hay un test que falla si el cliente de telemetría queda por dentro del traductor (comprobado invirtiendo el orden en el apply).
- **En un sistema real**, el contenido iría, si acaso, a un almacén con control de acceso y retención propios. El OTLP del panel no tiene autenticación: es una herramienta de desarrollo y solo se publica en `127.0.0.1`.

## Referencias

- Microsoft Learn, [clase `OpenTelemetryChatClient`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient): convenciones semánticas GenAI v1.41, experimentales (consultado el 2026-11-06).
- Microsoft Learn, [OpenTelemetry con OTLP y el Aspire Dashboard](https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example): herramienta de desarrollo, token de acceso en la UI (consultado el 2026-11-06).
- OpenTelemetry, [convenciones semánticas para IA generativa](https://opentelemetry.io/docs/specs/semconv/gen-ai/).
- Specs `agent-observability`, `chat-client` ("Errores del proveedor sin filtrar la clave") y `container-build` ("Endpoint de telemetría del runner vacío por defecto").
- Documento de aprendizaje [13, observabilidad de agentes](../learn/13-observabilidad-de-agentes.md).
