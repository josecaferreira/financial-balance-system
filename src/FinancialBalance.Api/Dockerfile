FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialBalance.Api/FinancialBalance.Api.csproj", "FinancialBalance.Api/"]
COPY ["src/FinancialBalance.Application/FinancialBalance.Application.csproj", "FinancialBalance.Application/"]
COPY ["src/FinancialBalance.Domain/FinancialBalance.Domain.csproj", "FinancialBalance.Domain/"]
COPY ["src/FinancialBalance.Infrastructure/FinancialBalance.Infrastructure.csproj", "FinancialBalance.Infrastructure/"]

RUN dotnet restore "FinancialBalance.Api/FinancialBalance.Api.csproj"

COPY src/ .
WORKDIR /src/FinancialBalance.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FinancialBalance.Api.dll"]
