namespace Sandbox.Domain.Results;

/// <summary>
/// Tipo de un error de dominio. Permite a las capas superiores decidir cómo reaccionar (por ejemplo, si un reintento
/// tiene sentido) sin depender del código concreto.
/// </summary>
public enum ErrorKind
{
    /// <summary>La petición está mal formada: corregirla antes de repetirla.</summary>
    Validation,

    /// <summary>El recurso pedido no existe.</summary>
    NotFound,

    /// <summary>La petición es válida, pero una regla de negocio la impide.</summary>
    BusinessRule,

    /// <summary>La petición choca con el estado actual (una transición o una clave ya usada).</summary>
    Conflict,

    /// <summary>Fallo pasajero: repetir la misma petición más tarde puede funcionar.</summary>
    Transient,
}
