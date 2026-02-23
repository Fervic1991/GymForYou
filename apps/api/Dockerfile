FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./apps/api/GymForYou.Api.csproj ./apps/api/
RUN dotnet restore ./apps/api/GymForYou.Api.csproj
COPY ./apps/api ./apps/api
WORKDIR /src/apps/api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "GymForYou.Api.dll"]
