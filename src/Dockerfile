FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.14 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.14 AS build
LABEL stage=build
ARG VERSION=0.0.1

WORKDIR /src
COPY . .

RUN dotnet restore Mentalist.ReverseProxy.sln
RUN dotnet build "./Mentalist.ReverseProxy/Mentalist.ReverseProxy.csproj" -c Release --no-restore /p:Version=${VERSION}
RUN dotnet publish "./Mentalist.ReverseProxy/Mentalist.ReverseProxy.csproj" -c Release --no-restore --no-build -o /app

FROM base AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Mentalist.ReverseProxy.dll"]
