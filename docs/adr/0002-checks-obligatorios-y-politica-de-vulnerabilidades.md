# ADR 0002: Checks obligatorios y política de vulnerabilidades de la imagen

- **Estado**: aceptada
- **Fecha**: 2026-10-31
- **Change**: `add-ci-pipeline` (decisiones D4 y D9 de su `design.md`)

## Contexto

La definición de terminado de `AGENTS.md` (sección 8) exigía validar las specs, compilar sin advertencias y pasar los tests, también en contenedor. Hasta este change solo se comprobaba a mano en el equipo de desarrollo, y nada impedía fusionar en `main` sin cumplirla.

Además, la imagen final se fija por digest (`add-container-build`). Eso hace el build reproducible, pero también **congela los parches**: si se publica una vulnerabilidad en un paquete de la imagen base, la imagen sigue teniéndola hasta que alguien actualiza el digest.

Un escáner de vulnerabilidades consulta una base de datos que cambia cada día. Por eso **su resultado no es determinista**: el mismo commit puede pasar hoy y fallar mañana. La decisión es cuándo ese cambio debe bloquear el trabajo.

## Decisión

1. **Tres checks obligatorios** en el ruleset `protect-main`, ligados a la aplicación GitHub Actions:
   - `test`: `docker build --target test`;
   - `image`: imagen final construida, ejecutada una vez y escaneada;
   - `openspec`: `openspec validate --all --strict`.

   Ningún PR se fusiona sin los tres en verde.
2. **Política del escaneo** (Grype, con la versión fijada):
   - el check **falla** con al menos una vulnerabilidad de severidad **alta o crítica con arreglo disponible**;
   - **no falla** por las que no tengan arreglo publicado ni por las de severidad media o inferior;
   - todas las encontradas se muestran en el registro del check.
3. La política se aplica a **todos** los PRs, no solo a los que tocan el `Dockerfile`.

La razón de `only-fixed` es que el check solo debe ponerse en rojo cuando hay algo que hacer: actualizar el digest de la imagen base o un paquete. Una vulnerabilidad sin arreglo bloquearía `main` sin salida posible.

## Alternativas consideradas

- **Solo informar** (subir el resultado como SARIF a *code scanning*): nunca bloquea. Necesita el permiso `security-events: write` y deja que `main` acumule vulnerabilidades con arreglo sin que nada obligue a actuar.
- **Bloquear todo desde severidad media** (el valor por defecto de la acción): pone en rojo PRs sin relación cuando no existe arreglo, y entrena a ignorar el check.
- **Escaneo obligatorio solo en los PRs que tocan el `Dockerfile`**: exigiría filtros de ruta, y un check obligatorio filtrado se queda "Pending" y bloquea el merge. Además, una vulnerabilidad nueva no llega con un cambio del `Dockerfile`, sino con la publicación del aviso.
- **Otro escáner**:
  - Trivy tiene el mismo modelo, pero su acción sufrió en marzo de 2026 un ataque que reetiquetó sus versiones (fuentes secundarias);
  - Docker Scout exige credenciales de Docker Hub, es decir, un secreto en la CI.

## Consecuencias

- **Un aviso nuevo puede bloquear un PR que no ha tocado nada relacionado**, incluso uno solo de documentación, hasta que se actualice el digest. Es el coste aceptado. Las actualizaciones de seguridad de Dependabot deberían traer ese cambio sin esperar a la revisión semanal.
- **La CI depende de servicios externos** (la base de datos de Grype). Si no están disponibles, el check falla y hay que reejecutarlo.
- **Renombrar un job obligatorio** sin actualizar el ruleset deja `main` bloqueada. Los nombres `test`, `image` y `openspec` son parte del contrato (spec `continuous-integration`).
- **Revisión**: si la política bloquea con demasiada frecuencia o deja pasar algo que debió frenar, se revisa con un change propio que sustituye este ADR.

## Referencias

- Spec [`continuous-integration`](../../openspec/specs/continuous-integration/spec.md); change archivado en `openspec/changes/archive/2026-10-31-add-ci-pipeline/`.
- Acción `anchore/scan-action`, entradas `severity-cutoff` y `only-fixed`: <https://github.com/anchore/scan-action>
- Checks obligatorios y filtros de ruta: <https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks>
- Documento de aprendizaje: [`docs/learn/09-integracion-continua-y-cadena-de-suministro.md`](../learn/09-integracion-continua-y-cadena-de-suministro.md).
