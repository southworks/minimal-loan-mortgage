FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/CohereLoanAndMortgage.AgentProvisioning.csproj", "agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/"]
RUN dotnet restore "agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/CohereLoanAndMortgage.AgentProvisioning.csproj"
COPY agent-provisioning/ agent-provisioning/
WORKDIR /src/agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning
RUN dotnet publish "CohereLoanAndMortgage.AgentProvisioning.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/agents /app/agents
COPY --from=build /src/agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/shared /app/shared
COPY --from=build /src/agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning/config/provisioning.json /app/config/provisioning.json
ENTRYPOINT ["dotnet", "CohereLoanAndMortgage.AgentProvisioning.dll", "--config", "/app/config/provisioning.json", "--agents", "/app/agents"]
