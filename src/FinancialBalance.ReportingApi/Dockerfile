FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialBalance.ReportingApi/FinancialBalance.ReportingApi.csproj", "FinancialBalance.ReportingApi/"]
COPY ["src/FinancialBalance.Application/FinancialBalance.Application.csproj", "FinancialBalance.Application/"]
COPY ["src/FinancialBalance.Domain/FinancialBalance.Domain.csproj", "FinancialBalance.Domain/"]
COPY ["src/FinancialBalance.ReportingInfrastructure/FinancialBalance.ReportingInfrastructure.csproj", "FinancialBalance.ReportingInfrastructure/"]

RUN dotnet restore "FinancialBalance.ReportingApi/FinancialBalance.ReportingApi.csproj"

COPY src/ .
WORKDIR /src/FinancialBalance.ReportingApi
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FinancialBalance.ReportingApi.dll"]
