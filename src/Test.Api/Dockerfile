FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY Directory.Build.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Learning/Learning.Contracts/ src/Learning/Learning.Contracts/
COPY src/Ai/Ai.Contracts/ src/Ai/Ai.Contracts/
COPY src/Test/ src/Test/

RUN dotnet restore src/Test/Test.Api/Test.Api.csproj
RUN dotnet publish src/Test/Test.Api/Test.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5005
EXPOSE 5005

ENTRYPOINT ["dotnet", "Test.Api.dll"]
