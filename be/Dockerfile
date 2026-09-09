FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG VERSION=0.1.0
ARG SHA=local
COPY . .
RUN dotnet publish src/Fantastiche.Application/Fantastiche.Application.csproj -c Release -o /app/publish -p:Version=${VERSION} -p:InformationalVersion=${VERSION}+${SHA}
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "Fantastiche.Application.dll"]
