# CLI de OpenSpec fijada, para la CI y para validar en local sin instalar Node.
# Node LTS por etiqueta y digest (verificado el 2026-10-31); Dependabot actualiza el digest.
# La versión de OpenSpec se sube a mano, junto con "openspec update" y los skills regenerados.
FROM node:26.13.0-alpine@sha256:8996bb5821d62a8fd9bb27c1cb07462f3fdce6e47d640c6a881ead0b064ce216

RUN npm install --global @fission-ai/openspec@1.16.0 && npm cache clean --force

# La CLI envía estadísticas de uso anónimas salvo que se desactiven (lo avisa al ejecutarse)
ENV OPENSPEC_TELEMETRY=0

USER node
WORKDIR /repo
ENTRYPOINT ["openspec"]
