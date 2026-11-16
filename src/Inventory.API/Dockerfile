FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/Inventory.Domain/Inventory.Domain.csproj", "src/Inventory.Domain/"]
COPY ["src/Inventory.Application/Inventory.Application.csproj", "src/Inventory.Application/"]
COPY ["src/Inventory.Infrastructure/Inventory.Infrastructure.csproj", "src/Inventory.Infrastructure/"]
COPY ["src/Inventory.API/Inventory.API.csproj", "src/Inventory.API/"]

RUN dotnet restore "src/Inventory.API/Inventory.API.csproj"

COPY ["src/", "src/"]
WORKDIR "/src/src/Inventory.API"
RUN dotnet publish "Inventory.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Inventory.API.dll"]
