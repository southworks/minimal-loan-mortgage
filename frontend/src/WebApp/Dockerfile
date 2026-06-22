FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore frontend/src/WebApp/Cohere.LoanProcessing.WebApp.csproj
RUN dotnet publish frontend/src/WebApp/Cohere.LoanProcessing.WebApp.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/dataset-seed /app/dataset-seed
ENV ASPNETCORE_URLS=http://+:8080
ENV DatasetSeed__RootPath=/app/dataset-seed
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cohere.LoanProcessing.WebApp.dll"]
