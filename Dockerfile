FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EMS.sln ./
COPY src/EMS.Domain/EMS.Domain.csproj src/EMS.Domain/
COPY src/EMS.Application/EMS.Application.csproj src/EMS.Application/
COPY src/EMS.Infrastructure/EMS.Infrastructure.csproj src/EMS.Infrastructure/
COPY src/EMS.Web/EMS.Web.csproj src/EMS.Web/
COPY tests/EMS.Tests/EMS.Tests.csproj tests/EMS.Tests/
RUN dotnet restore src/EMS.Web/EMS.Web.csproj

COPY . .
RUN dotnet publish src/EMS.Web/EMS.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EMS.Web.dll"]
