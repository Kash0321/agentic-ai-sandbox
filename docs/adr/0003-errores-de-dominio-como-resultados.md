# ADR 0003: Errores de dominio como resultados, no como excepciones

- **Estado**: aceptada
- **Fecha**: 2026-11-01
- **Change**: `add-order-domain` (decisión D2 de su `design.md`)

## Contexto

En la fase 3, el dominio de ventas valida pedidos, aplica reglas de negocio y controla las transiciones de estado. En la fase 4, esas operaciones se exponen al modelo como tools, y `AGENTS.md` (sección 4.4) prohíbe que una tool lance excepciones de negocio o de validación hacia el LLM: debe devolver un `ToolError` con un código estable y un indicador de si reintentar tiene sentido.

Hay que decidir cómo comunica el dominio que una regla no se cumple. La decisión no es local a este change:

- la fase 4 traduce cada fallo del dominio a un `ToolError`;
- la fase 5 decide qué guardrails actúan según el tipo de error (por ejemplo, no reintentar un error de validación);
- la fase 6 evalúa si el agente reacciona bien a cada código.

## Decisión

Las reglas de negocio y las validaciones del dominio **devuelven un resultado** en lugar de lanzar excepciones:

```
Result<T>   = un valor  o  un DomainError, nunca los dos
DomainError = (Code, Message, Kind)
ErrorKind   = Validation | NotFound | BusinessRule | Conflict | Transient
```

- `Result<T>` es un tipo propio y mínimo de `Sandbox.Domain` (`src/Sandbox.Domain/Results/`), sin dependencias ni encadenamiento monádico (`Bind`, `Map`) mientras nadie lo necesite.
- `Code` es un texto estable en inglés y en mayúsculas (`INSUFFICIENT_STOCK`), y todos viven en `ErrorCodes`, sin cadenas sueltas. Un test comprueba que son únicos y tienen ese formato.
- `Message` es breve y accionable, sin trazas ni nombres internos. Solo lleva los datos que necesita quien corrige la petición: el producto y su disponible en `INSUFFICIENT_STOCK`, y el producto en `PRODUCT_NOT_FOUND`.
- `Kind` clasifica el error para que las capas superiores reaccionen sin conocer cada código. La fase 4 derivará `Retryable` de `Kind == Transient`, sin que el dominio conozca `ToolError`.
- Las excepciones quedan para lo que **no** es una regla incumplida: un argumento nulo, un invariante roto por un error de programación (por ejemplo, liberar más stock del reservado) o la cancelación (`OperationCanceledException`), que se propaga siempre.

## Alternativas consideradas

- **Excepciones tipadas** (`InsufficientStockException`, etc.). Es el estilo habitual en .NET y deja limpio el camino feliz, pero:
  - el flujo de error no aparece en la firma, así que nada obliga a quien llama a tratarlo;
  - basta con olvidar un `catch` en una tool para que la excepción llegue al LLM, justo lo que prohíbe `AGENTS.md`;
  - un fallo esperado (stock insuficiente) se trata con el mismo mecanismo que uno inesperado (un error de programación), y separarlos exige disciplina en cada `catch`.
- **Librerías de resultados** (`ErrorOr`, `FluentResults`, `OneOf`). Dan más funciones (errores múltiples, encadenamiento), pero añaden una dependencia al dominio para unas 40 líneas de código, y `AGENTS.md` pide no añadir dependencias "por si acaso".
- **Códigos de error sin tipo** (solo `Code` y `Message`). Más simple, pero obliga a la fase 4 a mantener una tabla de qué códigos se pueden reintentar, y esa tabla se desincroniza al añadir códigos.

## Consecuencias

- **Más explícito, algo más verboso**: cada llamada comprueba `IsSuccess` antes de usar `Value`. Los atributos `MemberNotNullWhen` permiten al compilador avisar si se usa `Value` sin comprobarlo.
- **Errores deterministas**: el mismo incumplimiento da siempre el mismo código y tipo. Es la base para que el agente reaccione de forma predecible y para los escenarios de evaluación de la fase 6.
- **Las tools de la fase 4 serán finas**: `Result<T>` se traduce a `ToolResult<T>` con un mapeo directo, sin `try`/`catch` de negocio. El `catch` del borde de la tool queda solo para lo inesperado (`INTERNAL_ERROR`).
- **Un error nuevo exige tres cosas**: la constante en `ErrorCodes`, su fábrica con mensaje en `SalesErrors` (u otra clase de errores) y su escenario en la spec.
- **Revisión**: si aparece la necesidad de acumular varios errores en una sola respuesta (por ejemplo, validar todas las líneas a la vez), se amplía `Result<T>` o se revisa este ADR.

## Referencias

- `AGENTS.md`, sección 4 (reglas de tools: sin excepciones hacia el LLM, códigos estables).
- Specs `order-domain` (requisito "Errores de negocio como resultado") y `order-operations`.
- Documento de aprendizaje: [`docs/learn/11-dominios-aptos-para-agentes.md`](../learn/11-dominios-aptos-para-agentes.md), sección del patrón *Result*.
- Microsoft Learn, *Best practices for exceptions* (consultado el 2026-11-01): usar excepciones si el evento "is truly exceptional", y comprobar la condición en el código "if the event happens routinely and could be considered part of normal execution". Un pedido sin stock es rutinario: https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions
- Microsoft Learn, *Exceptions and Performance* (extracto de las *Framework Design Guidelines*, 2.ª edición, 2008; consultado el 2026-11-01): "DO NOT use error codes because of concerns that exceptions might affect performance". Esta decisión no se toma por rendimiento, sino porque el error es parte del contrato con el LLM y debe aparecer en la firma: https://learn.microsoft.com/dotnet/standard/design-guidelines/exceptions-and-performance
