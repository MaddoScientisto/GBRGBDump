# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["GBRGBDump.Web/GBRGBDump.Web.csproj", "GBRGBDump.Web/"]
COPY ["GBRGBDump.Web.Shared/GBRGBDump.Web.Shared.csproj", "GBRGBDump.Web.Shared/"]
COPY ["GBTools.Bootstrapper/GBTools.Bootstrapper.csproj", "GBTools.Bootstrapper/"]
COPY ["GBTools.Common/GBTools.Common.csproj", "GBTools.Common/"]
COPY ["GBTools.Decoder/GBTools.Decoder.csproj", "GBTools.Decoder/"]
COPY ["GBTools.Graphics/GBTools.Graphics.csproj", "GBTools.Graphics/"]
RUN dotnet restore "./GBRGBDump.Web/GBRGBDump.Web.csproj"
COPY . .
WORKDIR "/src/GBRGBDump.Web"
RUN dotnet build "./GBRGBDump.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GBRGBDump.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GBRGBDump.Web.dll"]