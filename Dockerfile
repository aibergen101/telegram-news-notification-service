FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TelegramApi/TelegramApi.csproj", "TelegramApi/"]
COPY ["TelegramApi.Application/TelegramApi.Application.csproj", "TelegramApi.Application/"]
COPY ["TelegramApi.Domain/TelegramApi.Domain.csproj", "TelegramApi.Domain/"]
COPY ["TelegramApi.Infrastructure/TelegramApi.Infrastructure.csproj", "TelegramApi.Infrastructure/"]

RUN dotnet restore "TelegramApi/TelegramApi.csproj"

COPY . .
WORKDIR "/src/TelegramApi"
RUN dotnet build "TelegramApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
&& rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramApi.dll"]
