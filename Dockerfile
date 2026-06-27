# Stage 1: Build Vue frontend
FROM node:22-alpine AS frontend-build
WORKDIR /src/HamStats.Vue

COPY HamStats.Vue/package.json HamStats.Vue/package-lock.json ./
RUN npm ci

COPY HamStats.Vue/ ./
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG version=0.0.0
ARG gitsha=unknown
WORKDIR /src

COPY HamStats.Data/ HamStats.Data/
COPY HamStats.Website/ HamStats.Website/
RUN shortsha=$(printf '%.8s' "$gitsha") \
    && echo "Building version $version+$shortsha from $gitsha" \
    && dotnet publish HamStats.Website/HamStats.Website.csproj \
        -c Release \
        -o /app/publish \
        /p:Version=$version \
        /p:EnableSourceControlManagerQueries=false \
        /p:SourceRevisionId=$shortsha \
        /p:IncludeSourceRevisionInInformationalVersion=false \
        /p:InformationalVersion=$version+$shortsha

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=backend-build /app/publish .
COPY --from=frontend-build /src/HamStats.Vue/build ./wwwroot

# Bind to all interfaces (appsettings.json pins localhost, which is unreachable in a container)
ENV Kestrel__Endpoints__Http__Url="http://+:5000"
ENV ASPNETCORE_ENVIRONMENT="Production"
# SQLite database and downloaded callsign/postal dumps live in /data so they persist on the mounted volume
ENV ConnectionStrings__Default="Data Source=/data/hamstats.db"
ENV CallsignLookup__CacheDirectory="/data/callsign-cache"

# HTTP dashboard / API
EXPOSE 5000
# N1MM+ UDP broadcast ingestion
EXPOSE 12060/udp

VOLUME ["/data"]

ENTRYPOINT ["dotnet", "HamStats.Website.dll"]
