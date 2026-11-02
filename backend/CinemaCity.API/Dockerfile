FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CinemaCity.slnx Directory.Build.props ./
COPY backend/CinemaCity.Domain/CinemaCity.Domain.csproj backend/CinemaCity.Domain/
COPY backend/CinemaCity.Application/CinemaCity.Application.csproj backend/CinemaCity.Application/
COPY backend/CinemaCity.Infrastructure/CinemaCity.Infrastructure.csproj backend/CinemaCity.Infrastructure/
COPY backend/CinemaCity.ServiceDefaults/CinemaCity.ServiceDefaults.csproj backend/CinemaCity.ServiceDefaults/
COPY backend/CinemaCity.API/CinemaCity.API.csproj backend/CinemaCity.API/
RUN dotnet restore backend/CinemaCity.API/CinemaCity.API.csproj

COPY . .
RUN dotnet publish backend/CinemaCity.API/CinemaCity.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CinemaCity.API.dll"]
