ARG PROJECT=CinemaCity.API
ARG PROJECT_FOLDER=backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
ARG PROJECT_FOLDER
WORKDIR /src
COPY Directory.Build.props ./
COPY backend/ backend/
COPY tools/ tools/
RUN dotnet publish ${PROJECT_FOLDER}/${PROJECT}/${PROJECT}.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PROJECT
WORKDIR /app
ENV APP_DLL=${PROJECT}.dll
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
