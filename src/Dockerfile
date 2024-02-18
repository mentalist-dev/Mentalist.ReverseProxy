FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
LABEL stage=build
ARG VERSION=0.0.1

WORKDIR /src
COPY . .

RUN dotnet restore Mentalist.ReverseProxy.sln
RUN dotnet build "./Mentalist.ReverseProxy/Mentalist.ReverseProxy.csproj" -c Release --no-restore /p:Version=${VERSION}
RUN dotnet publish "./Mentalist.ReverseProxy/Mentalist.ReverseProxy.csproj" -c Release --no-restore --no-build -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS="http://+:80;http://+:443"
ENV Routing__ForceHttps="true"
ENV Routing__HttpPort="80"
ENV Routing__HttpsPort="443"
ENV Routing__HttpsScheme="https"

COPY --from=build /app .

RUN apk update --no-cache && apk upgrade --no-cache

ENTRYPOINT ["dotnet", "lb.dll"]
