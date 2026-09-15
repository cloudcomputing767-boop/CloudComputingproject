# =============================================================================
# Dockerfile for the ASP.NET Core API
#
# WHY IS THIS HERE?
# Render can run Node, Python, Ruby, Go, Rust and Elixir on its own, but it has
# no built-in support for .NET. For .NET it runs a container instead, so this
# file is the recipe that tells Render how to build and start our API.
#
# It is written in two stages so the final image stays small:
#   Stage 1 "build"  - has the full .NET SDK (compilers, ~800 MB) and compiles
#                      the project.
#   Stage 2 "final"  - has only the ASP.NET runtime (~220 MB) and receives the
#                      compiled output from stage 1. The SDK is thrown away.
# =============================================================================

# ---------- Stage 1: compile ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file on its own first and restore the NuGet packages.
# Docker caches this step, so packages are only downloaded again when
# backend.csproj actually changes - not on every code edit.
COPY backend.csproj ./
RUN dotnet restore

# Now copy the rest of the source and publish a release build.
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Stage 2: run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Render tells the container which port to listen on through the PORT
# environment variable, and Program.cs reads it. This value is only the
# fallback for running the container locally.
ENV PORT=8080
EXPOSE 8080

# Run as a non-root user - good practice, and the image already provides one.
USER $APP_UID

ENTRYPOINT ["dotnet", "backend.dll"]
