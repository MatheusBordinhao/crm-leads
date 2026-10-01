# syntax=docker/dockerfile:1

# Imagem de produção do CRM: a API servindo o build do React, mais o migration bundle do EF, que o serviço
# "migracoes" do docker-compose.prod.yml roda antes de a API subir.

# 1. Front: o build do React, que vai para o wwwroot da API.
FROM node:22-alpine AS front
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2. API: o publish e o migration bundle, que aplica as migrations pendentes e termina.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json .editorconfig ./
COPY src/Crm.Api/Crm.Api.csproj src/Crm.Api/
RUN dotnet restore src/Crm.Api/Crm.Api.csproj && dotnet tool restore
COPY src/ src/
RUN dotnet publish src/Crm.Api/Crm.Api.csproj --configuration Release --no-restore --output /publicado
RUN dotnet ef migrations bundle --project src/Crm.Api/Crm.Api.csproj --configuration Release --output /publicado/efbundle

# 3. A imagem final: só o runtime do ASP.NET Core, com o usuário sem root que a imagem traz.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /publicado ./
COPY --from=front /web/dist ./wwwroot

# A pasta das chaves da sessão vira um volume. Criada aqui, o volume já nasce com o dono certo.
RUN mkdir -p /dados/chaves && chown "$APP_UID" /dados/chaves
USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__Pasta=/dados/chaves
EXPOSE 8080
ENTRYPOINT ["dotnet", "Crm.Api.dll"]
