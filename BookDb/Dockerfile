# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj và restore dependencies
COPY ["BookDb/BookDb.csproj", "BookDb/"]
RUN dotnet restore "BookDb/BookDb.csproj"

# Copy toàn bộ source code
COPY . .
WORKDIR "/src/BookDb"

# Build ứng dụng
RUN dotnet build "BookDb.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "BookDb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Cài đặt dependencies cho PDF processing (nếu cần)
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Tạo thư mục uploads
RUN mkdir -p /app/wwwroot/uploads && chmod 777 /app/wwwroot/uploads

# Expose ports
EXPOSE 80
EXPOSE 443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "BookDb.dll"]