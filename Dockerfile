FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY nuget.config Directory.Build.props ./
COPY src/ src/

RUN dotnet restore src/Test.Api/Test.Api.csproj
RUN dotnet publish src/Test.Api/Test.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5005
EXPOSE 5005

ENTRYPOINT ["dotnet", "Test.Api.dll"]
