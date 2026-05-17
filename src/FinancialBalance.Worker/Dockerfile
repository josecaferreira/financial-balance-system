FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialBalance.Worker/FinancialBalance.Worker.csproj", "FinancialBalance.Worker/"]
COPY ["src/FinancialBalance.Application/FinancialBalance.Application.csproj", "FinancialBalance.Application/"]
COPY ["src/FinancialBalance.Domain/FinancialBalance.Domain.csproj", "FinancialBalance.Domain/"]

RUN dotnet restore "FinancialBalance.Worker/FinancialBalance.Worker.csproj"

COPY src/ .
WORKDIR /src/FinancialBalance.Worker
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FinancialBalance.Worker.dll"]
