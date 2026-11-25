## Build Stage
# Build in a separate stage to avoid copying the SDK into the final image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore packages
COPY ["AsyncTickets.TicketRenderer/AsyncTickets.TicketRenderer.csproj", "AsyncTickets.TicketRenderer/"]
COPY ["AsyncTickets.Messaging/AsyncTickets.Messaging.csproj", "AsyncTickets.Messaging/"]
COPY ["AsyncTickets.Models/AsyncTickets.Models.csproj", "AsyncTickets.Models/"]
RUN dotnet restore "./AsyncTickets.TicketRenderer/AsyncTickets.TicketRenderer.csproj"

# Build and publish
COPY . .
WORKDIR "/src/AsyncTickets.TicketRenderer"
RUN dotnet publish "./AsyncTickets.TicketRenderer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

## Runtime Stage
# Chiseled images contain only the minimal set of packages needed for .NET 10.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 8080

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "./AsyncTickets.TicketRenderer.dll"]
