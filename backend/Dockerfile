FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/InsuranceClaims.Api/InsuranceClaims.Api.csproj

RUN dotnet publish src/InsuranceClaims.Api/InsuranceClaims.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

CMD ["sh", "-c", "dotnet InsuranceClaims.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
