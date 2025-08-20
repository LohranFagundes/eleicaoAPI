#!/bin/bash

# ==============================================================================
# Election API .NET - Docker Quick Update Script v1.1.1
# Updated: 02/08/2024
# ==============================================================================

echo "🔄 Quick Docker Update for Election API v1.1.1"
echo "📅 Updated: 02/08/2024"
echo ""

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

# Stop existing containers
print_status "Stopping existing containers..."
docker-compose down

# Remove old images
print_status "Removing old images..."
docker image prune -f
docker rmi $(docker images -q --filter "reference=*election*") 2>/dev/null || true

# Rebuild and start
print_status "Rebuilding with latest changes..."
docker-compose up --build -d

print_success "✅ Update complete! Check logs with: docker-compose logs -f"
print_success "🌐 API available at: http://localhost:5110"
print_success "📚 Swagger at: http://localhost:5110/swagger"