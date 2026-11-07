# Imágenes fijadas por etiqueta y digest del índice multiplataforma (verificadas el 2026-10-31).
# Manda el digest; la etiqueta solo informa. Actualizar ambos a la vez.

# restore: solo los ficheros que definen dependencias, para que un cambio de código no invalide esta capa
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS restore
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props AgenticSandbox.slnx ./
# Un COPY por proyecto: si falta uno, la restauración de la solución falla de forma explícita
COPY src/Sandbox.Domain/Sandbox.Domain.csproj src/Sandbox.Domain/
COPY src/Sandbox.Application/Sandbox.Application.csproj src/Sandbox.Application/
COPY src/Sandbox.Agents/Sandbox.Agents.csproj src/Sandbox.Agents/
COPY src/Sandbox.Runner/Sandbox.Runner.csproj src/Sandbox.Runner/
COPY tests/Sandbox.Domain.Tests/Sandbox.Domain.Tests.csproj tests/Sandbox.Domain.Tests/
COPY tests/Sandbox.Application.Tests/Sandbox.Application.Tests.csproj tests/Sandbox.Application.Tests/
COPY tests/Sandbox.Agents.Tests/Sandbox.Agents.Tests.csproj tests/Sandbox.Agents.Tests/
COPY tests/Sandbox.Scenarios.Tests/Sandbox.Scenarios.Tests.csproj tests/Sandbox.Scenarios.Tests/
RUN dotnet restore AgenticSandbox.slnx

FROM restore AS build
COPY .editorconfig ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet build AgenticSandbox.slnx --no-restore -c Release

# test: lo que ejecuta "docker build --target test ." y la CI; sin red, como exige build-standards
FROM build AS test
RUN --network=none dotnet test --solution AgenticSandbox.slnx --no-build -c Release

FROM build AS publish
RUN dotnet publish src/Sandbox.Runner/Sandbox.Runner.csproj --no-build -c Release -o /app

# final: runtime chiseled (sin shell ni gestor de paquetes), usuario no root 1654 heredado de la base
FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble-chiseled@sha256:9ff26250358cef92587a087a7bef8d5655893385ff921ad9d5e924f20602fad4 AS final
WORKDIR /app
COPY --from=publish /app ./
ENTRYPOINT ["dotnet", "Sandbox.Runner.dll"]
