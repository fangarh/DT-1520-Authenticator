FROM node:22-alpine AS build
WORKDIR /src/admin

COPY admin/package.json admin/package-lock.json ./
RUN npm ci

COPY admin ./

ARG VITE_ADMIN_API_BASE_URL=
ENV VITE_ADMIN_API_BASE_URL=$VITE_ADMIN_API_BASE_URL

RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime

COPY infra/nginx/admin.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/admin/dist /usr/share/nginx/html

EXPOSE 8443
