FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["backend/src/LoanWorkflow.Mcp/LoanWorkflow.Mcp.csproj", "backend/src/LoanWorkflow.Mcp/"]
RUN dotnet restore "backend/src/LoanWorkflow.Mcp/LoanWorkflow.Mcp.csproj"
COPY backend/src/LoanWorkflow.Mcp/ backend/src/LoanWorkflow.Mcp/
COPY dataset-seed/ dataset-seed/
WORKDIR /src/backend/src/LoanWorkflow.Mcp
RUN dotnet publish "LoanWorkflow.Mcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/dataset-seed /app/dataset-seed
ENV Dataset__RootPath=/app/dataset-seed
ENV Dataset__PolicyFilePath=/app/dataset-seed/08_policy_rag/general_policy.txt
ENTRYPOINT ["dotnet", "LoanWorkflow.Mcp.dll"]
