FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["hosted-agents/src/CohereLoanAndMortgage.HostedAgents/CohereLoanAndMortgage.HostedAgents.csproj", "hosted-agents/src/CohereLoanAndMortgage.HostedAgents/"]
RUN dotnet restore "hosted-agents/src/CohereLoanAndMortgage.HostedAgents/CohereLoanAndMortgage.HostedAgents.csproj"
COPY hosted-agents/ hosted-agents/
RUN dotnet publish "hosted-agents/src/CohereLoanAndMortgage.HostedAgents/CohereLoanAndMortgage.HostedAgents.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8088
ENV ASPNETCORE_URLS=http://+:8088
ENTRYPOINT ["dotnet", "CohereLoanAndMortgage.HostedAgents.dll"]
