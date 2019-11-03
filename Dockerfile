# STAGE01 - Build application and its dependencies
FROM mcr.microsoft.com/dotnet/core/sdk:3.0-alpine AS build
WORKDIR /app

COPY . ./
RUN dotnet restore

# STAGE02 - Publish the application
FROM build AS publish
WORKDIR /app/Net.Bluewalk.AnyRestApi2Mqtt
RUN dotnet publish -c Release -o ../out
RUN rm ../out/*.pdb

# STAGE03 - Create the final image
FROM mcr.microsoft.com/dotnet/core/runtime:3.0-alpine AS runtime
LABEL Description="Any REST API to MQTT image" \
      Maintainer="Bluewalk"

WORKDIR /app
COPY --from=publish /app/out ./

ENTRYPOINT ["dotnet", "Net.Bluewalk.AnyRestApi2Mqtt.dll"]
