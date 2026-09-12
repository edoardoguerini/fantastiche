# Frontend SSR: Node rende le pagine; nginx serve asset, API e WebSocket.

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

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV API_UPSTREAM=http://localhost:6060
RUN apk add --no-cache nginx gettext supervisor \
    && corepack enable && corepack prepare pnpm@10.33.1 --activate
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --prod --frozen-lockfile
COPY --from=builder /app/dist ./dist
COPY nginx ./nginx
COPY server ./server
USER node
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz > /dev/null || exit 1
CMD ["sh", "-c", "envsubst '${API_UPSTREAM}' < /app/nginx/default.conf.template > /tmp/fantastiche-server.conf && exec supervisord -c /app/server/supervisord.conf"]
