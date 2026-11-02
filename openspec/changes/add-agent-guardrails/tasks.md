# Tasks

Rama de trabajo: `feat/add-agent-guardrails`. Referencias: `design.md` (decisiones D1 a D12 y experimentos E1 a E7 del explore) y `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red ni clave. No hay paquetes nuevos: si la implementación necesitara alguno, se verifica en la API de NuGet y se anota la fecha (`AGENTS.md`, sección 11).

## 1. Auditoría base (`Sandbox.Application` y dobles de test)

- [ ] 1.1 Crear la categoría `Sandbox.Audit` y los eventos con `[LoggerMessage]` e identificadores estables: `ToolInvoked`, `UnknownToolRequested`, `RunStopped` y `AccessDenied` (D4). Test: cada evento sale en la categoría `Sandbox.Audit`, con su identificador y sus campos.
- [ ] 1.2 Hacer que `RecordingLoggerProvider` (en `Sandbox.Agents.Tests`, enlazado en `Sandbox.Scenarios.Tests`) y `RecordingLogger` (en `Sandbox.Application.Tests`) capturen los *scopes* activos en cada entrada. Test del doble: una entrada registrada dentro de dos *scopes* anidados lleva las propiedades de ambos, y una registrada fuera no lleva ninguna.

## 2. Autorización por recurso e idempotencia por ámbito (`Sandbox.Application`)

- [ ] 2.1 Introducir el ámbito de idempotencia (cliente o almacén) e indexar `IdempotencyStore` por ámbito y clave (D3). Ajustar los tests existentes que dependan del ámbito global, citando D3. Tests de "Ámbito de las claves de idempotencia":
  - "Dos clientes con la misma clave";
  - los escenarios de idempotencia y conflicto existentes, que siguen en verde.
- [ ] 2.2 Añadir el cliente a `GetOrderQuery` y a `CancelOrderCommand`, y comprobar la pertenencia en `SalesQueries` y en `SalesCommands`, dentro de la sección crítica, con el mismo error que un pedido inexistente (D1, D2). Actualizar los decoradores del inyector y los tests que construyen esos tipos. Tests:
  - "Cancelar el pedido de otro cliente" (estado y stock sin cambios, mismo mensaje que `ORD-9999`);
  - "Consultar el pedido de otro cliente";
  - "Otro cliente repite la misma petición".
- [ ] 2.3 Emitir `AccessDenied` en las denegaciones por pedido ajeno. Tests de "Auditoría de los accesos denegados": "Acceso a un pedido ajeno" (operación, pedido, cliente y motivo) y "Pedido inexistente" (sin evento).
- [ ] 2.4 Escribir el ADR 0005 (autorización por recurso en la aplicación, pedido ajeno indistinguible de uno inexistente, claves de idempotencia por ámbito; D1 a D3). Actualizar el ADR 0004: la limitación conocida queda resuelta y enlaza al 0005. Actualizar el documento 06: el código `ORDER_NOT_FOUND` en lugar de `FORBIDDEN`, la trampa de la idempotencia como ejemplo de orden de comprobaciones y los escenarios previstos para la fase 6. Añadir al glosario "enumeración de recursos" y "ámbito de idempotencia". Verificación: los enlaces entre los ADR y el documento 06 resuelven.

## 3. Tools (`Sandbox.Agents`)

- [ ] 3.1 Pasar el cliente de sesión a `GetOrderQuery` y a `CancelOrderCommand` en `SalesTools`. Tests:
  - "Pedido de otro cliente" de `get_order`: el mensaje es igual que el de `ORD-9999`, sin datos del pedido;
  - "Pedido de otro cliente" de `cancel_order`: el pedido sigue en `Placed`;
  - los casos existentes de las dos tools, que siguen en verde.
- [ ] 3.2 Añadir los códigos `BLOCKED_BY_POLICY` y `UNKNOWN_TOOL` a `ToolErrorCodes` y marcar en `SalesToolNames` las tools de lectura (D5). Test: la lista de lectura es exactamente `list_products`, `get_stock`, `get_customer` y `get_order`, y todas están en el catálogo.

## 4. Opciones de los guardrails (`Sandbox.Agents`)

- [ ] 4.1 Crear `GuardrailOptions` (sección `Guardrails`, con `AllowedTools` anulable) y su validador (D5). Tests del validador:
  - valores por defecto válidos;
  - cada límite menor que 1, con un mensaje que nombra el ajuste;
  - lista vacía;
  - tool que no existe (`ship_order`);
  - y la resolución de `AllowedTools` nulo a las seis tools.

## 5. Guardrails del agente (`Sandbox.Agents`)

- [ ] 5.1 Test de aprendizaje: qué hace `FunctionInvokingChatClient` cuando el `FunctionInvoker` pone `Terminate` en la primera de dos tools pedidas en la misma respuesta. Anotar el resultado en `design.md` (pregunta abierta).
- [ ] 5.2 Sustituir `CallCountingChatClient` por `RunBudgetChatClient`, que cuenta las llamadas y los tokens y lanza `GuardrailStopException` antes de superar `MaxModelCalls` o `TokenBudget`. Fijar `MaximumIterationsPerRequest` a `MaxModelCalls` (D6). Tests del cliente:
  - corta antes de la llamada N+1;
  - corta cuando el acumulado alcanza el presupuesto;
  - no libera el cliente interior.
- [ ] 5.3 Añadir `AgentStopReason` al resultado, los textos fijos por motivo y la captura de `GuardrailStopException` en `SalesAgent`, con tokens y llamadas del cliente de presupuesto (D6, D10). Tests:
  - "Modelo que no deja de pedir tools";
  - "Respuesta en la última llamada permitida";
  - "Presupuesto agotado";
  - "Ejecución dentro del presupuesto";
  - "Parada por un guardrail" del bucle.
- [ ] 5.4 Contar en el `FunctionInvoker` los errores seguidos y cortar con `Terminate` (D7). Tests:
  - "Tres errores seguidos";
  - "Un éxito reinicia la cuenta";
  - "Parada por errores seguidos" (texto fijo, sin el texto del modelo, evento `RunStopped`);
  - "Resultado parcial tras una parada".
- [ ] 5.5 Aplicar la allow-list: las tools permitidas van en `ChatOptions.Tools`, el resto en `AdditionalTools`, y el `FunctionInvoker` devuelve `BLOCKED_BY_POLICY` sin invocar (D8). Tests:
  - "Solo se ofrecen las permitidas";
  - "Tool no permitida pedida igualmente" (sin pedido y sin cambio de stock);
  - "Tools disponibles para el modelo" con la configuración por defecto.
- [ ] 5.6 Detectar las tools desconocidas al terminar y construir la lista de tools en el orden de las peticiones (D9). Tests: "Nombre inventado" y "Nombre demasiado largo".
- [ ] 5.7 Abrir el *scope* de auditoría (`RunId` y `CustomerId`) y emitir `ToolInvoked` (con la decisión) en lugar de la línea de registro actual (D4). Tests:
  - "Invocación fallida";
  - "Invocación bloqueada";
  - "Correlación de una ejecución" (incluido el `AccessDenied` de la aplicación);
  - "Dos ejecuciones";
  - "Sin datos de la petición".
- [ ] 5.8 Rechazar en `RunAsync` las peticiones de más de `MaxRequestLength` caracteres (D11). Tests: "Petición demasiado larga" (sin llamadas al modelo) y que la de 2.000 caracteres se acepta.
- [ ] 5.9 Actualizar el documento 05: los guardrails implementados, la tabla de dónde actúa cada uno, los experimentos E1 a E7 (qué cubren los límites de la librería y qué no), el motivo de parada y la auditoría correlacionada, con los errores típicos. Añadir al glosario "presupuesto de tokens", "motivo de parada" y "correlación". Verificación: cada afirmación sobre la librería cita la documentación consultada el 2026-11-02 o el spike.

## 6. Runner (`Sandbox.Runner`)

- [ ] 6.1 Registrar `GuardrailOptions` con su validador y `ValidateOnStart`, y aplicar la allow-list del escenario con `PostConfigure` (D5). Añadir el escenario `read-only` al catálogo. Tests:
  - "Valores por defecto";
  - "Límite inválido";
  - "Tool permitida que no existe";
  - "El escenario no amplía la allow-list";
  - una intersección vacía da el código 2.
- [ ] 6.2 Añadir `ExitCodes.StoppedByGuardrail = 4`, la línea del motivo de parada en la salida, el mensaje por la salida de errores e `IncludeScopes` en la consola (D12). Tests:
  - "Parada por un guardrail" (código 4, mensaje con `ModelCallLimit`);
  - "Escenario feliz con cliente falso" (con `Completed`);
  - "Solo lectura con cliente falso" y "Solo lectura" de extremo a extremo (sin pedido, disponible 20);
  - que una línea registrada durante la ejecución muestra el identificador de ejecución.
- [ ] 6.3 Validar la longitud de `--prompt` con la constante del agente (D11). Tests: "Texto demasiado largo" (código 2, el mensaje nombra la opción) y que las peticiones de todos los escenarios caben.
- [ ] 6.4 Actualizar `README.md` (sección `Guardrails`, código 4 y escenario `read-only`), `docs/plan.md` (fase 5 en tres changes, con el estado de este), `docs/learn/README.md` (índice y fila de la fase 5) y el backlog con los temas que surjan. Verificación: los comandos de ejemplo del `README.md` usan opciones que existen.

## 7. Comprobación de integración

- [ ] 7.1 `dotnet build` sin advertencias y `dotnet test` con todos los tests en verde.
- [ ] 7.2 `openspec validate --all --strict` sin errores.
- [ ] 7.3 `docker build --target test` en verde. Imagen final sin clave: termina con el código 2. Comprobar que los canarios de `secrets/` y `.env` no llegan a la imagen. Al terminar, cerrar Docker Desktop y ejecutar `wsl --shutdown`.
- [ ] 7.4 Ejecuciones reales contra DeepSeek: `happy-order`, `lost-response` y `read-only`, y una con un presupuesto bajo (`Guardrails:TokenBudget`) que termine con el código 4. Anotar en el documento 05 las llamadas, los tokens y lo que hace el modelo ante `BLOCKED_BY_POLICY` y ante la parada.

## Workflow follow-up

- Ejecutar `/opsx:verify` y corregir lo crítico.
- Archivar con `/opsx:archive` en la misma rama, con las specs principales sincronizadas.
- Commit, push y PR solo cuando el usuario lo pida. El PR debe tener en verde los checks `test`, `image` y `openspec`.
