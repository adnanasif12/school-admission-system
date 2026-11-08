FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until a csproj changes.
COPY SchoolAdmission.sln ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.Tests/SchoolAdmission.Tests.csproj tests/SchoolAdmission.Tests/
RUN dotnet restore src/SchoolAdmission.Api/SchoolAdmission.Api.csproj

COPY src/ src/
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
