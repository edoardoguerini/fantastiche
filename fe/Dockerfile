# Immagine di produzione del frontend Fantastiche: build SPA + nginx.
# nginx serve gli asset statici e inoltra /api e /hubs all'API (stessa origin).

FROM node:24-alpine AS builder
WORKDIR /app
RUN corepack enable && corepack prepare pnpm@10.33.1 --activate
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
# Vuoto = stessa origin del sito (nginx fa da proxy). Build-time: inlineato da Vite.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ARG VITE_APP_VERSION=0.0.0
ENV VITE_APP_VERSION=${VITE_APP_VERSION}
RUN pnpm build

FROM nginxinc/nginx-unprivileged:1.31-alpine AS runtime
# L'entrypoint ufficiale rende /etc/nginx/templates/*.template in /etc/nginx/conf.d.
ENV NGINX_ENVSUBST_FILTER=^API_UPSTREAM$
ENV API_UPSTREAM=http://localhost:6060
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=builder /app/dist/client /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/_shell.html > /dev/null || exit 1
