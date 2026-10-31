# CLI de OpenSpec fijada, para la CI y para validar en local sin instalar Node.
# Node LTS por etiqueta y digest (verificado el 2026-10-31); Dependabot actualiza el digest.
# La versión de OpenSpec se sube a mano, junto con "openspec update" y los skills regenerados.
FROM node:26.13.0-alpine@sha256:c6ad9e9c1d80f6eb607c26b4b0a132ff7a2344fad1c78ea3baa2a7d4c8de6d94

RUN npm install --global @fission-ai/openspec@1.16.0 && npm cache clean --force

# La CLI envía estadísticas de uso anónimas salvo que se desactiven (lo avisa al ejecutarse)
ENV OPENSPEC_TELEMETRY=0

USER node
WORKDIR /repo
ENTRYPOINT ["openspec"]
