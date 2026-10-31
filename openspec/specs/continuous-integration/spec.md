# continuous-integration Specification

## Purpose
Define la verificación automática de cada pull request y de `main`: qué se comprueba, con qué garantías de seguridad se ejecuta, qué checks son obligatorios para fusionar y cómo se trata una vulnerabilidad en la imagen. También cubre la medición manual del coste de trabajar en contenedor.

## Requirements

### Requirement: Verificación automática de cada cambio
El repositorio SHALL ejecutar la CI en cada pull request contra `main` y en cada push a `main`, sea cual sea el fichero modificado. Los workflows que publican checks obligatorios MUST NOT usar filtros de rama ni de ruta que puedan impedir que el check se publique.

#### Scenario: PR que solo cambia documentación
- **GIVEN** un pull request contra `main` que solo modifica ficheros Markdown
- **WHEN** se abre o se actualiza el pull request
- **THEN** se ejecutan todos los checks obligatorios y publican su resultado

#### Scenario: Push a main
- **GIVEN** un merge en `main`
- **WHEN** GitHub registra el push
- **THEN** se ejecutan los mismos checks sobre el commit resultante

### Requirement: Tests de la solución en contenedor
La CI SHALL publicar un check `test` que construye la etapa de tests del `Dockerfile` del repositorio, el mismo que se usa en local. El check MUST fallar si falla la compilación o algún test.

#### Scenario: Tests en verde
- **GIVEN** un pull request con todos los tests en verde
- **WHEN** se ejecuta el check `test`
- **THEN** el check termina con éxito

#### Scenario: Un test en rojo
- **GIVEN** un pull request con un test que falla
- **WHEN** se ejecuta el check `test`
- **THEN** el check falla y su registro identifica el test fallido

### Requirement: Imagen final verificada
La CI SHALL publicar un check `image` que construye la imagen final del `Dockerfile` y la ejecuta una vez sin argumentos. El check MUST fallar si la construcción falla o si la ejecución no termina con código 0.

#### Scenario: La imagen arranca
- **GIVEN** un pull request cuya imagen final se construye
- **WHEN** se ejecuta el check `image`
- **THEN** el Runner termina con código 0 dentro del contenedor y el check continúa con el escaneo

### Requirement: Política de vulnerabilidades de la imagen
El check `image` SHALL escanear la imagen final en busca de vulnerabilidades conocidas. MUST fallar cuando haya alguna de severidad alta o crítica con arreglo disponible, y MUST NOT fallar por las que no tengan arreglo publicado ni por las de severidad inferior. Todas las encontradas SHALL quedar en el registro del check.

#### Scenario: Vulnerabilidad alta con arreglo
- **GIVEN** una imagen final con una vulnerabilidad de severidad alta para la que existe una versión corregida
- **WHEN** se ejecuta el escaneo
- **THEN** el check `image` falla e identifica la vulnerabilidad y el paquete afectado

#### Scenario: Vulnerabilidad sin arreglo
- **GIVEN** una imagen final cuyas únicas vulnerabilidades altas o críticas no tienen arreglo publicado
- **WHEN** se ejecuta el escaneo
- **THEN** el check `image` termina con éxito y el registro muestra esas vulnerabilidades

#### Scenario: Escáner reproducible
- **GIVEN** la definición de la CI
- **WHEN** se revisa cómo se obtiene el escáner
- **THEN** su versión está fijada explícitamente y no depende de la última publicada

### Requirement: Validación de las especificaciones
La CI SHALL publicar un check `openspec` que valida todas las especificaciones y changes en modo estricto, con una versión fijada de la CLI de OpenSpec. El check MUST fallar si la validación encuentra errores.

#### Scenario: Specs válidas
- **GIVEN** un pull request cuyas specs y changes son válidos en modo estricto
- **WHEN** se ejecuta el check `openspec`
- **THEN** el check termina con éxito

#### Scenario: Requisito sin escenario
- **GIVEN** un pull request con un requisito de spec sin ningún escenario
- **WHEN** se ejecuta el check `openspec`
- **THEN** el check falla e identifica el requisito

### Requirement: Permisos mínimos y sin secretos
Los workflows SHALL ejecutarse con un token de solo lectura del contenido del repositorio, salvo que un job concreto declare un permiso adicional justificado. MUST NOT usar secretos del repositorio ni eventos que ejecuten código de un fork con permisos de escritura o con acceso a secretos.

#### Scenario: Token de solo lectura
- **GIVEN** cualquier workflow de la CI
- **WHEN** se revisan sus permisos efectivos
- **THEN** el token solo puede leer el contenido del repositorio

#### Scenario: PR desde un fork
- **GIVEN** un pull request abierto desde un fork
- **WHEN** se ejecuta la CI
- **THEN** se ejecuta sin acceso a secretos y sin permisos de escritura sobre el repositorio

### Requirement: Acciones fijadas por SHA
Toda acción de terceros usada por un workflow SHALL referenciarse por el SHA completo de su commit, con la versión legible en un comentario. El repositorio SHALL estar configurado para rechazar workflows que referencien acciones por etiqueta o rama.

#### Scenario: Revisión de los workflows
- **GIVEN** los workflows del repositorio
- **WHEN** se revisan sus referencias a acciones
- **THEN** cada una usa un SHA de 40 caracteres seguido de un comentario con la versión

#### Scenario: Acción referenciada por etiqueta
- **GIVEN** la configuración del repositorio que exige SHA
- **WHEN** un workflow referencia una acción por etiqueta
- **THEN** GitHub se niega a ejecutarlo

### Requirement: Checks obligatorios para fusionar en main
El ruleset de `main` SHALL exigir que los checks `test`, `image` y `openspec` terminen con éxito antes de fusionar. Los checks MUST estar ligados a la aplicación GitHub Actions, para que no los pueda satisfacer otra integración que publique un check con el mismo nombre.

#### Scenario: Check en rojo
- **GIVEN** un pull request con uno de los checks obligatorios fallido
- **WHEN** se intenta fusionar
- **THEN** GitHub no permite el merge

#### Scenario: Todos los checks en verde
- **GIVEN** un pull request con los tres checks obligatorios en verde y el resto de reglas del ruleset cumplidas
- **WHEN** se intenta fusionar con *squash*
- **THEN** GitHub permite el merge

### Requirement: Medición manual nativo frente a contenedor
El repositorio SHALL proporcionar un workflow que solo se ejecuta a petición y que mide, en un mismo runner, el tiempo de los tests nativos y el de la etapa de tests en contenedor, en frío y en caliente. Su resultado SHALL quedar visible en el resumen de la ejecución. El workflow MUST NOT ser un check obligatorio.

#### Scenario: Ejecución a petición
- **GIVEN** el workflow de medición
- **WHEN** se lanza manualmente
- **THEN** el resumen de la ejecución muestra los tiempos nativo y en contenedor, en frío y en caliente

#### Scenario: No interfiere con los PR
- **GIVEN** un pull request cualquiera
- **WHEN** se ejecuta la CI
- **THEN** el workflow de medición no se ejecuta y no figura entre los checks obligatorios
