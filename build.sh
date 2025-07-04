#!/bin/bash

echo "Building Perfect World Manager..."

# Clean previous builds
echo "Cleaning previous builds..."
find . -type d -name "bin" -exec rm -rf {} + 2>/dev/null || true
find . -type d -name "obj" -exec rm -rf {} + 2>/dev/null || true

# Build the daemon first (this generates the gRPC code)
echo "Building PerfectWorldManagerDaemon..."
dotnet build PerfectWorldManagerDaemon/PerfectWorldManagerDaemon.csproj

# Copy the generated gRPC files to Core project if needed
echo "Ensuring gRPC files are available..."

# Build Core project
echo "Building PerfectWorldManager.Core..."
dotnet build PerfectWorldManager.Core/PerfectWorldManager.Core.csproj

# Build GUI project
echo "Building PerfectWorldManager.Gui..."
dotnet build PerfectWorldManager.Gui/PerfectWorldManager.Gui.csproj

echo "Build complete!"