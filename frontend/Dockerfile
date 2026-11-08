# ---- build stage -------------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# ---- runtime stage -----------------------------------------------------------
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

# Entry script that generates config.js from API_URL at container start.
# The sed strips CRLF line endings (e.g. a Windows checkout) which would otherwise break the
# shebang with "exec ... no such file or directory".
COPY docker-entrypoint.sh /docker-entrypoint.d/40-runtime-config.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-runtime-config.sh \
 && chmod +x /docker-entrypoint.d/40-runtime-config.sh

ENV API_URL=http://localhost:8080
EXPOSE 80
