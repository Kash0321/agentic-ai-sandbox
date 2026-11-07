# Tasks

Rama de trabajo: `feat/add-agent-observability`. Referencias: `design.md` (decisiones D1 a D12 y experimentos A1 a A10 y D1 a D6 del explore) y `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red, clave ni receptor OTLP. Los spans se capturan con un oyente en memoria, en tests sin paralelismo (D11).

## 1. Dependencias

- [x] 1.1 Añadir a `Directory.Packages.props` `OpenTelemetry.Extensions.Hosting` y `OpenTelemetry.Exporter.OpenTelemetryProtocol`. Antes, volver a comprobar en la API de NuGet que 1.20.1 sigue siendo la última estable (verificada el 2026-11-06) y anotar la fecha en el comentario del fichero. Referenciarlos solo desde `Sandbox.Runner`. Comprobación: `dotnet build` sin avisos y la licencia Apache-2.0 en NuGet.

## 2. Oyente de spans y tests de aprendizaje

- [x] 2.1 Crear en `Sandbox.Agents.Tests/Fakes` un oyente de spans de prueba sobre las fuentes indicadas, que muestrea todo y guarda los spans que terminan mientras está vivo, y la colección sin paralelismo `TelemetrySerialGroup` (D11). Tests del propio oyente: guarda los spans de sus fuentes, ignora los de otras y no guarda los que terminan después de liberarlo.
- [x] 2.2 Fijar en `Sandbox.Agents.Tests/Learning` el comportamiento observado de la librería (A2a, A2b, A3 y A4):
  - sin span raíz, `orchestrate_tools` raíz y dos trazas con una pausa de aprobación;
  - con span raíz, sin `orchestrate_tools` y con `execute_tool` hijos del raíz;
  - `OpenTelemetryChatClient` copia el mensaje de la excepción en el estado del span;
  - con span raíz y `EnableSensitiveData`, `execute_tool` sin `gen_ai.tool.call.arguments`.

  Comprobación: los cuatro tests en verde.

## 3. Cliente del modelo con telemetría (`Sandbox.Agents`)

- [x] 3.1 Crear la constante de la fuente `Sandbox.Agents` (D1) y `TelemetryOptions` (sección `Telemetry`, `CaptureContent` falso por defecto), con su enlace en el Runner (D5). Test: sin la sección, `CaptureContent` es falso.
- [x] 3.2 Añadir `UseOpenTelemetry` en `DeepSeekChatClientFactory`, por fuera de `ProviderErrorTranslatingChatClient`, sin `ILoggerFactory`, con la fuente del agente y `EnableSensitiveData` según `CaptureContent` (D2, D5). Tests con `StubHttpMessageHandler`:
  - el escenario "Clave rechazada con telemetría" de `chat-client` (span en error, ningún fragmento de la clave ni texto del proveedor en estado, atributos o eventos);
  - una llamada correcta produce un span `chat` con el modelo y los tokens;
  - con `CaptureContent`, el span lleva los mensajes; sin él, no.

  Los tests existentes de la factoría siguen en verde.

## 4. Trazas del agente (`Sandbox.Agents`)

- [x] 4.1 Permitir que `AgentHarness` envuelva el modelo con `UseOpenTelemetry` sobre la fuente del agente, con y sin captura de contenido (D11). Comprobación: los tests actuales del agente siguen en verde con y sin envoltura.
- [x] 4.2 Abrir en `SalesAgent.RunAsync` el span raíz `invoke_agent sales-agent`, que envuelve todas las vueltas, con `sandbox.run.id` igual al `RunId` del *scope* y, al cerrar, el motivo de parada, las llamadas y los tokens. Estado de error si para un guardrail o sale una excepción, sin `AddException` (D3). Tests de `agent-observability`:
  - "Atributos del span raíz";
  - "Parada por un guardrail";
  - una excepción del modelo deja el raíz en error con el nombre del tipo y sin su mensaje;
  - el `RunId` del span coincide con el de la auditoría.
- [x] 4.3 Abrir el span `approval <tool>` alrededor de cada decisión del aprobador, con la tool y la decisión y sin argumentos (D3). Tests: "Una sola traza con confirmación", "Confirmación aprobada" y "Confirmación denegada".
- [x] 4.4 Añadir `sandbox.tool.outcome` al span `execute_tool` en curso desde el `FunctionInvoker` (D4). Tests: "Tool con error de negocio" (`ORDER_NOT_FOUND`), "Tool bloqueada" (`BLOCKED_BY_POLICY`), éxito con `OK`, y "Dos llamadas" de los spans `chat`.
- [x] 4.5 Tests de contenido con el agente: "Pedido sin contenido en la traza" (ni la petición, ni las instrucciones, ni `MUG-RED`, ni la clave de idempotencia en ningún atributo o evento) y "Mensajes en la traza" (con captura, la petición, los argumentos y el resultado de `place_order` en los spans `chat`).

## 5. Exportación en el Runner (`Sandbox.Runner`)

- [x] 5.1 Leer `OTEL_EXPORTER_OTLP_ENDPOINT` de la configuración y validarla al arrancar si tiene valor (URL absoluta `http` o `https`; si no, código 2 nombrando la opción) (D6). Tests: "Endpoint que no es una URL", "Esquema no admitido" y "Endpoint vacío" (arranca sin exportación).
- [x] 5.2 Con endpoint válido, registrar OpenTelemetry: recurso `sandbox-runner` sin identificador de instancia, trazas y métricas de la fuente del agente, logs con *scopes* y mensaje formateado, y `UseOtlpExporter()`. Sin endpoint, no registrar nada (D6, D8). Comprobar que el exportador toma el endpoint de la configuración del host; si no, pasárselo explícito (plan B de D6). Tests:
  - "Sin endpoint" (no hay `TracerProvider` en el contenedor y el código es 0);
  - con endpoint, el `TracerProvider` existe;
  - "Misma salida con y sin exportación", con un endpoint sin receptor y un cliente falso. Si el vaciado contra un endpoint sin receptor hace el test demasiado lento, se baja el tiempo de espera del exportador solo en el test.
- [x] 5.3 Registrar tras arrancar la línea `Telemetría OTLP activa hacia {Endpoint}` (solo esquema, host y puerto) y, con `CaptureContent`, el aviso de contenido (D5, D6). Tests: "Arranque con endpoint", "Arranque sin endpoint", "Aviso al arrancar" y que un endpoint con credenciales embebidas no las muestra.
- [x] 5.4 Test de "La consola no muestra el contenido": `--scenario happy-order` con captura activada y cliente falso, sin la petición del escenario en los registros.

## 6. Compose y Dependabot

- [x] 6.1 Añadir a `compose.yaml` el servicio `dashboard` en el perfil `dashboard` (D9). Antes, volver a comprobar en MCR la última estable (13.6.2 el 2026-11-06; 13.6.4 el 2026-11-07, que es la que se fija) y tomar su digest del registro. Añadir al `runner` `OTEL_EXPORTER_OTLP_ENDPOINT: ${OTEL_EXPORTER_OTLP_ENDPOINT:-}` y actualizar el comentario de cabecera con el uso. Comprobación: `docker compose config` sin errores, con y sin `--profile dashboard`.
- [x] 6.2 Añadir a `.github/dependabot.yml` el ecosistema `docker-compose` en `/`, semanal y agrupado (D10). Comprobación: YAML válido; la de GitHub queda para después del merge (pendiente en el documento 09).
- [x] 6.3 Verificación en contenedor del panel:
  - el escenario "Sin perfil no hay panel";
  - "Panel endurecido en marcha" con `docker inspect` (usuario, solo lectura, capacidades, `no-new-privileges`, límites, puertos en `127.0.0.1`);
  - "Imagen del panel fijada".

  Anotar el resultado en el documento 07.

## 7. Verificación de extremo a extremo

- [x] 7.1 `docker build --target test` en verde. Revisar la imagen final: ni variables ni historial con secretos ni endpoints, y el `runner` sin endpoint termina con código 2 sin clave, como en la CI.
- [x] 7.2 Con el panel arriba y `OTEL_EXPORTER_OTLP_ENDPOINT=http://dashboard:18889`, ejecutar `docker compose run --rm runner --scenario happy-order` contra DeepSeek (clave por fichero). Comprobar en el panel:
  - una sola traza con raíz, `chat` y `execute_tool`;
  - los registros con mensaje formateado y enlace a la traza;
  - las métricas de tokens;
  - el recurso `sandbox-runner`, si se agrupa (pregunta abierta de D8).

  Lo lanza el usuario o se hace con él. Se anotan tiempos y tokens. Hecho el 2026-11-07: traza, atributos y recurso vistos en el panel; las métricas, comprobadas en el envío OTLP (con un receptor de prueba), no en la vista del panel.
- [x] 7.3 Con `dotnet run` y `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`, ejecutar `--scenario cancel-order` con aprobación real en consola (lo lanza el usuario con `!`) y comprobar en el panel el span `approval` con la espera humana dentro de la misma traza.
- [x] 7.4 Ejecutar una vez con `Telemetry__CaptureContent=true` y comprobar en el panel los mensajes en los spans `chat`, y en la consola el aviso y la ausencia de contenido.

## 8. Documentación

- [x] 8.1 Crear `docs/learn/13-observabilidad-de-agentes.md`:
  - trazas, spans y contexto; logs correlacionados; métricas; OTLP y exportadores;
  - convenciones semánticas GenAI (experimentales, v1.41 según la referencia de la clase);
  - datos sensibles y la captura opcional;
  - el error del proveedor en el estado del span (A3) y por qué el orden del pipeline importa;
  - el span raíz y la pausa de aprobación (A2);
  - procesos *one-shot*, vaciado y el coste de un endpoint sin receptor (A8, A9, D5);
  - el mensaje formateado (A10);
  - lo observado en 7.2 a 7.4, con fechas de verificación y lo no verificado marcado.

  Diagramas en Mermaid.
- [x] 8.2 Actualizar el documento 07 (perfil `dashboard`, endurecimiento con `tmpfs` y su motivo, puertos solo locales, OTLP sin autenticación, memoria observada, uso) y el 09 (ecosistema `docker-compose`, sin actualizaciones de seguridad, pendiente de ver el primer PR).
- [x] 8.3 Actualizar el documento 05 (la auditoría exportada y enlazada con su traza) y el 04 (`UseOpenTelemetry` usado, sin `ILoggerFactory`, y la variable estándar de contenido sin efecto en 10.11.0).
- [x] 8.4 Escribir `docs/adr/0006-telemetria-opcional-y-sin-contenido.md` (D12).
- [x] 8.5 Ampliar `docs/learn/glosario.md` (span, traza, contexto de traza, recurso, convenciones semánticas, exportador, *flush*, muestreo). Actualizar en `docs/learn/README.md` el índice (documento 13), el mapa de la fase 5 y el *backlog* de observabilidad. Proponer al cerrar la fase los temas nuevos que merezca la pena documentar.
- [x] 8.6 Actualizar `docs/plan.md` (fila del tercer change de la fase 5 y cierre de la fase) y `README.md` (estado y uso del panel).

## 9. Cierre

- [x] 9.1 `dotnet build` sin avisos, `dotnet test` en verde y `openspec validate --all --strict` sin errores. Medir la longitud de las descripciones de los requisitos MODIFIED de `chat-client` y `dependency-updates` (no pasan de 500 caracteres en la spec principal).

## Workflow follow-up

- `/opsx:verify` sin problemas críticos.
- `/opsx:archive` en la rama del change, con las specs principales sincronizadas, antes de abrir el PR.
- Tras fusionar: comprobar el primer pull request de Dependabot del ecosistema `docker-compose` y anotarlo en el documento 09.
