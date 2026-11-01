# Spec Delta

## MODIFIED Requirements

### Requirement: Imagen final verificada
La CI SHALL publicar un check `image` que construye la imagen final del `Dockerfile` y la ejecuta una vez sin argumentos y sin API key. El check MUST fallar si la construcción falla o si la ejecución no termina con el código de salida de configuración inválida (2), que es el esperado sin clave.

#### Scenario: La imagen arranca
- **GIVEN** un pull request cuya imagen final se construye
- **WHEN** se ejecuta el check `image`
- **THEN** el Runner termina con código 2 dentro del contenedor, sin red ni clave, y el check continúa con el escaneo

#### Scenario: La imagen no arranca
- **GIVEN** un pull request cuyo Runner falla antes de validar la configuración (por ejemplo, con un error inesperado y código 1)
- **WHEN** se ejecuta el check `image`
- **THEN** el check falla
