# Image unique contenant l'API et l'ETL (voir docs/infrastructure.md).
# Commande par défaut : API ; le service "etl" surcharge l'entrée par "dotnet Etl.dll".
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/Api/Api.csproj src/Api/
COPY src/Etl/Etl.csproj src/Etl/
RUN dotnet restore src/Api/Api.csproj \
 && dotnet restore src/Etl/Etl.csproj

COPY . .
RUN dotnet publish src/Api/Api.csproj -c Release -o /out --no-restore \
 && dotnet publish src/Etl/Etl.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
CMD ["dotnet", "Api.dll"]
