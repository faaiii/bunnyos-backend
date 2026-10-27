# ==========================================
# BunnyOS Cloud Backend - Sub-directory Dockerfile
# ==========================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BunnyOS.Domain/BunnyOS.Domain.csproj", "BunnyOS.Domain/"]
COPY ["BunnyOS.Application/BunnyOS.Application.csproj", "BunnyOS.Application/"]
COPY ["BunnyOS.Infrastructure/BunnyOS.Infrastructure.csproj", "BunnyOS.Infrastructure/"]
COPY ["BunnyOS.Ai/BunnyOS.Ai.csproj", "BunnyOS.Ai/"]
COPY ["BunnyOS.Api/BunnyOS.Api.csproj", "BunnyOS.Api/"]

RUN dotnet restore "BunnyOS.Api/BunnyOS.Api.csproj"

COPY . .

WORKDIR "/src/BunnyOS.Api"
RUN dotnet publish "BunnyOS.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=5000;8080;10000
EXPOSE 5000 8080 10000

ENTRYPOINT ["dotnet", "BunnyOS.Api.dll"]
