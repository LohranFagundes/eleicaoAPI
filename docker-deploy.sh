#!/bin/bash

# ==============================================================================
# Election API .NET - Docker Deployment Script v1.1.1
# Updated: 02/08/2024
# ==============================================================================

echo "🚀 Starting Election API .NET Docker Deployment"
echo "📅 Updated: 02/08/2024"
echo "🔖 Version: 1.1.1"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    print_error "Docker is not running. Please start Docker and try again."
    exit 1
fi

print_status "Docker is running ✓"

# Check if .env file exists
if [ ! -f .env ]; then
    print_warning ".env file not found. Creating template..."
    cat > .env << EOF
# Database Configuration
DB_ROOT_PASSWORD=ElectionSystem2024!
DB_DATABASE=election_system
DB_USERNAME=root
DB_PASSWORD=ElectionSystem2024!

# JWT Configuration
JWT_SECRET_KEY=your-super-secret-jwt-key-256-bits-minimum-for-production
JWT_ISSUER=ElectionApi
JWT_AUDIENCE=ElectionApiUsers
JWT_EXPIRE_MINUTES=60

# SMTP Configuration (for 2FA emails)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your-app-password
SMTP_FROM_EMAIL=noreply@election-system.com
SMTP_FROM_NAME=Sistema de Eleições

# Vote Encryption Keys
VOTE_MASTER_KEY=your-vote-encryption-master-key-must-be-very-secure
VOTE_JUSTIFICATION_KEY=your-justification-encryption-key-secure

# Swagger Configuration
SWAGGER_USERNAME=admin
SWAGGER_PASSWORD=swagger-admin-password

# Environment
ASPNETCORE_ENVIRONMENT=Production

# Hybrid Photo System Settings
MAX_PHOTO_SIZE_MB=10
PHOTO_QUALITY=85
MAX_PHOTO_WIDTH=800
MAX_PHOTO_HEIGHT=600
EOF
    print_warning "Please edit the .env file with your actual configuration before proceeding."
    print_warning "Especially: JWT_SECRET_KEY, SMTP settings, and encryption keys."
    read -p "Press Enter to continue after editing .env file..."
fi

print_status "Environment file exists ✓"

# Stop existing containers
print_status "Stopping existing containers..."
docker-compose down --volumes 2>/dev/null || true

# Remove old images to ensure fresh build
print_status "Removing old images..."
docker image prune -f 2>/dev/null || true
docker rmi $(docker images -q --filter "reference=*election*") 2>/dev/null || true

# Build and start services
print_status "Building and starting services..."
if docker-compose up --build -d; then
    print_success "Services started successfully!"
else
    print_error "Failed to start services. Check logs with: docker-compose logs"
    exit 1
fi

# Wait for services to be ready
print_status "Waiting for services to be ready..."
sleep 10

# Check service health
print_status "Checking service health..."

# Check MySQL
if docker-compose exec -T db mysqladmin ping -h localhost -u root -p${DB_PASSWORD:-ElectionSystem2024!} > /dev/null 2>&1; then
    print_success "MySQL is healthy ✓"
else
    print_warning "MySQL might not be ready yet. This is normal on first run."
fi

# Check API
sleep 15  # Give API more time to start
if curl -f http://localhost:5110/health > /dev/null 2>&1; then
    print_success "API is healthy ✓"
    
    # Get API info
    print_status "API Information:"
    curl -s http://localhost:5110/ | head -20
else
    print_warning "API might not be ready yet. Check logs with: docker-compose logs api"
fi

echo ""
print_success "===================================================="
print_success "🎉 Election API Deployment Complete!"
print_success "===================================================="
echo ""
print_status "📋 Service Information:"
print_status "  🌐 API URL: http://localhost:5110"
print_status "  📚 Swagger: http://localhost:5110/swagger"
print_status "  🏥 Health Check: http://localhost:5110/health"
print_status "  🗄️  MySQL: localhost:3306"
echo ""
print_status "🔧 Useful Commands:"
print_status "  📊 View logs: docker-compose logs -f"
print_status "  📊 API logs: docker-compose logs -f api"
print_status "  📊 DB logs: docker-compose logs -f db"
print_status "  🔄 Restart: docker-compose restart"
print_status "  🛑 Stop: docker-compose down"
echo ""
print_status "🆕 NEW Features in v1.1.1:"
print_status "  🔐 Two-Factor Authentication (2FA)"
print_status "  🔒 System Seal cryptographic verification"
print_status "  📊 Enhanced audit and reporting"
print_status "  📸 Hybrid photo system with ImageSharp"
print_status "  👥 Complete admin management"
echo ""
print_warning "⚠️  Remember to:"
print_warning "  1. Configure your .env file properly"
print_warning "  2. Set up SMTP for 2FA emails"
print_warning "  3. Use strong encryption keys in production"
print_warning "  4. Backup your database regularly"
echo ""
print_success "🎯 Ready to use! Import the Postman collection for testing."