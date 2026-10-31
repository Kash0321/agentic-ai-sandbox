# Imágenes fijadas por etiqueta y digest del índice multiplataforma (verificadas el 2026-10-31).
# Manda el digest; la etiqueta solo informa. Actualizar ambos a la vez.

# restore: solo los ficheros que definen dependencias, para que un cambio de código no invalide esta capa
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS restore
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props AgenticSandbox.slnx ./
# Un COPY por proyecto: si falta uno, la restauración de la solución falla de forma explícita
COPY src/Sandbox.Domain/Sandbox.Domain.csproj src/Sandbox.Domain/
COPY src/Sandbox.Application/Sandbox.Application.csproj src/Sandbox.Application/
COPY src/Sandbox.Agents/Sandbox.Agents.csproj src/Sandbox.Agents/
COPY src/Sandbox.Runner/Sandbox.Runner.csproj src/Sandbox.Runner/
COPY tests/Sandbox.Domain.Tests/Sandbox.Domain.Tests.csproj tests/Sandbox.Domain.Tests/
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
FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble-chiseled@sha256:3eb54ac708855f74b032de48e02437d15fb083b2aa593d49fdd512bb17f3e944 AS final
WORKDIR /app
COPY --from=publish /app ./
ENTRYPOINT ["dotnet", "Sandbox.Runner.dll"]
