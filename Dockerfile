# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file and restore dependencies for all projects
COPY TifSnippetApp.sln ./
COPY TifSnippetApp/TifSnippetApp.csproj ./TifSnippetApp/
COPY TifSnippetApp.Client/TifSnippetApp.Client.csproj ./TifSnippetApp.Client/
RUN dotnet restore

# Copy the remaining source files and publish
COPY TifSnippetApp/ ./TifSnippetApp/
COPY TifSnippetApp.Client/ ./TifSnippetApp.Client/
RUN dotnet publish TifSnippetApp/TifSnippetApp.csproj -c Release -o /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Configure ASP.NET Core to bind to port 8080 (the default since .NET 8)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TifSnippetApp.dll"]
