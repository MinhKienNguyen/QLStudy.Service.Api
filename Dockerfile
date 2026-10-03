FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY QLStudy.API.slnx ./
COPY src/QLStudy.Domain/QLStudy.Domain.csproj src/QLStudy.Domain/
COPY src/QLStudy.Application/QLStudy.Application.csproj src/QLStudy.Application/
COPY src/QLStudy.Infrastructure/QLStudy.Infrastructure.csproj src/QLStudy.Infrastructure/
COPY src/QLStudy.Service.Api/QLStudy.API.csproj src/QLStudy.Service.Api/

RUN dotnet restore src/QLStudy.Service.Api/QLStudy.API.csproj

COPY . .

RUN dotnet publish src/QLStudy.Service.Api/QLStudy.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=app:app /app/publish .

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "QLStudy.API.dll"]
