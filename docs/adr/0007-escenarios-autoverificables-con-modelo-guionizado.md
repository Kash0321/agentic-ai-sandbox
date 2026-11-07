# ADR 0007: Escenarios autoverificables con modelo guionizado en el Runner

- **Estado**: aceptada
- **Fecha**: 2026-11-07
- **Change**: `add-scenario-engine` (decisiones D1, D3, D4, D5, D7 y D11 de su `design.md`)

## Contexto

La fase 6 pide una matriz de escenarios documentada y ejecutable. Hasta este change, un escenario solo fijaba su preparación (cliente, petición, fallos, allow-list y pedidos previos). Lo que hacía el modelo y lo que se comprobaba al final vivían en los tests, así que:

- desde el Runner solo se podía ejecutar contra DeepSeek, con clave y red, y el resultado había que interpretarlo a mano;
- con un modelo real no se puede forzar un mal comportamiento, y para enseñar que la arquitectura contiene a un modelo que se equivoca o que es engañado, el modelo tiene que equivocarse;
- el responsable del laboratorio quiere que el producto final sirva para hacer demostraciones al equipo, y lo que solo vive en `dotnet test` no se puede enseñar.

Los changes siguientes de la fase (escenarios nuevos, *prompt injection* y repeticiones contra DeepSeek) se construyen sobre la misma decisión, por eso se registra aquí.

## Decisión

1. **Un escenario se comprueba a sí mismo.** Además de su preparación, lleva una descripción, un guion y comprobaciones. Al terminar, el Runner las evalúa y escribe un veredicto, con el modelo real o con el guion.
2. **Las comprobaciones son de dos tipos.**
   - **Seguridad**: nada malo ha pasado, sea cual sea el camino del modelo. Si falla una, el código de salida es **5**, con prioridad sobre el 4.
   - **Objetivo**: lo pedido se ha conseguido. Depende del modelo y solo se informa.

   El motor añade a todos los escenarios las comprobaciones comunes: el stock cuadra con los pedidos vivos y no hay pedidos nuevos de otro cliente.
3. **El modelo con guion sustituye solo al cliente del proveedor.** El bucle, los guardrails, la aprobación, la auditoría y la telemetría son los reales. Se elige con `--model scripted` y no necesita clave ni red.
4. **El modelo con guion se identifica siempre**: con una primera línea `MODELO GUIONIZADO` en la salida y con el proveedor `scripted` en los spans y los registros.
5. **Las comprobaciones leen el estado con una lectura de inspección** de la aplicación, sin autorización por cliente, que **nunca se ofrece como tool**.

## Alternativas consideradas

- **Oráculo solo en los tests**: menos código de producción, pero nada se puede enseñar fuera de `dotnet test` y la suite contra el modelo real no correría en el contenedor del Runner.
- **Sustituir el `IChatClient` entero**, como hacían los tests: se perderían los spans `chat` y el registro de uso, y la demo con guion no se vería igual en el panel que una real.
- **Respuestas HTTP grabadas del proveedor**: ejercitan también el adaptador de OpenAI, pero el guion sería JSON del protocolo, difícil de leer en una demostración, y seguiría exigiendo clave y endpoint.
- **Comprobaciones con las queries normales**: aplican la autorización por recurso y ocultan los pedidos de otros clientes, justo los que importan para comprobar que nadie actuó en su nombre.
- **Un único tipo de comprobación**: con solo invariantes de seguridad, un modelo que no hace nada daría "correcto"; con solo objetivos, el código de salida dependería de la calidad del modelo y la CI sería inestable.

## Consecuencias

- **El Runner es también una herramienta de demostración**, con un modelo falso en el código de producción (`Sandbox.Agents/Scripting`). Es explícito (opción, etiqueta y este ADR) y no tiene red ni secretos.
- **Añadir un escenario es añadir una entrada al catálogo.** La matriz de los tests y el check `image` de la CI lo recogen sin escribir un test ni tocar el workflow.
- **El código de salida 5 es nuevo** en la spec `runner-host`. Sin `--model`, todo sigue ejecutándose contra DeepSeek como antes; lo único visible es el bloque de comprobaciones al final de cada escenario.
- **El guion no dice nada del modelo real.** La calidad de DeepSeek se mide aparte, con las mismas comprobaciones y, más adelante, con repeticiones.
- **Las comprobaciones comunes pueden quedarse cortas o ser demasiado estrictas** cuando el dominio crezca (por ejemplo, con pedidos enviados). Se revisarán entonces.

## Referencias

- Specs `agent-scenarios`, `runner-host` ("Elección del modelo", "Códigos de salida"), `chat-client` ("Opciones de conexión validadas al arrancar"), `order-operations` ("Lectura de inspección") y `continuous-integration` ("Imagen final verificada").
- Documento de aprendizaje [14, pruebas y evaluación de sistemas agénticos](../learn/14-pruebas-y-evaluacion-de-sistemas-agenticos.md).
- ADR [0005, autorización por recurso en la aplicación](0005-autorizacion-por-recurso-en-la-aplicacion.md).
