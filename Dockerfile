# See https://aka.ms/customizecontainer to learn how to customize your debug container 
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# -------------------------------
# 1️⃣ Base runtime image
# -------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Install SkiaSharp native dependencies (Linux shared libs)
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libharfbuzz0b \
    libpng16-16 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    libxcb1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# (Optional) If $APP_UID is used for non-root execution, ensure the user exists
# USER $APP_UID

# -------------------------------
# 2️⃣ Build stage
# -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CountryCurrencyApi.csproj", "."]
RUN dotnet restore "./CountryCurrencyApi.csproj"

# Copy source and build
COPY . .
RUN dotnet build "./CountryCurrencyApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# -------------------------------
# 3️⃣ Publish stage
# -------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CountryCurrencyApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# -------------------------------
# 4️⃣ Final runtime image
# -------------------------------
FROM base AS final
WORKDIR /app

# Copy compiled app
COPY --from=publish /app/publish .

# Entry point
ENTRYPOINT ["dotnet", "CountryCurrencyApi.dll"]
