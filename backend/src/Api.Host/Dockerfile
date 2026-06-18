FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["backend/src/Api.Host/CohereLoanAndMortgage.Api.Host.csproj", "backend/src/Api.Host/"]
COPY ["backend/src/LoanWorkflow.Mcp/LoanWorkflow.Mcp.csproj", "backend/src/LoanWorkflow.Mcp/"]
RUN dotnet restore "backend/src/Api.Host/CohereLoanAndMortgage.Api.Host.csproj"
COPY backend/src/Api.Host/ backend/src/Api.Host/
COPY backend/src/LoanWorkflow.Mcp/ backend/src/LoanWorkflow.Mcp/
WORKDIR /src/backend/src/Api.Host
RUN dotnet publish "CohereLoanAndMortgage.Api.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CohereLoanAndMortgage.Api.Host.dll"]
