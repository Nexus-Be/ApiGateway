FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8000
ENV ASPNETCORE_URLS=http://+:8000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Nexus.Gateway/Nexus.Gateway.csproj", "Nexus.Gateway/"]
COPY ["Nexus.Gateway.Tests/Nexus.Gateway.Tests.csproj", "Nexus.Gateway.Tests/"]
RUN dotnet restore "Nexus.Gateway/Nexus.Gateway.csproj"
RUN dotnet restore "Nexus.Gateway.Tests/Nexus.Gateway.Tests.csproj"
COPY . .
RUN dotnet build "Nexus.Gateway/Nexus.Gateway.csproj" -c Release -o /app/build
RUN dotnet test "Nexus.Gateway.Tests/Nexus.Gateway.Tests.csproj" --no-restore -c Release --logger "trx;LogFileName=test-results.trx"

FROM build AS publish
RUN dotnet publish "Nexus.Gateway/Nexus.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nexus.Gateway.dll"]
