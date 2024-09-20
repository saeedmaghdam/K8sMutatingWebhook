#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["K8sMutatingWebhook/K8sMutatingWebhook.csproj", "K8sMutatingWebhook/"]
RUN dotnet restore "./K8sMutatingWebhook/K8sMutatingWebhook.csproj"
COPY . .
WORKDIR "/src/K8sMutatingWebhook"
RUN dotnet build "./K8sMutatingWebhook.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./K8sMutatingWebhook.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

ENV ASPNETCORE_URLS=https://+:5001
ENV ASPNETCORE_HTTPS_PORT=5001
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=Hello#rt
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/s3d-k8s-svc-default-k8smutatingwebhook-service-server.pfx

COPY --from=publish /app/publish .

COPY ./certs/*.pfx /app/certs/
COPY ./certs/*.crt /usr/local/share/ca-certificates/
COPY ./certs/*.key /usr/local/share/ca-certificates/
RUN update-ca-certificates

USER app

ENTRYPOINT ["dotnet", "K8sMutatingWebhook.dll"]