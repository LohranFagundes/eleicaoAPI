#!/bin/bash

# Election API .NET - Docker Update Script v1.3.0
# Script para atualizar a API no Docker com zero downtime

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
NEW_VERSION="1.3.0"
OLD_VERSION="1.2.2"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"

echo -e "${BLUE}===============================================${NC}"
echo -e "${BLUE}🚀 Election API .NET Docker Update v${NEW_VERSION}${NC}"
echo -e "${BLUE}===============================================${NC}"

# Check if required files exist
echo -e "${CYAN}📋 Verificando arquivos necessários...${NC}"
if [[ ! -f "$COMPOSE_FILE" ]]; then
    echo -e "${RED}❌ Arquivo $COMPOSE_FILE não encontrado${NC}"
    exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
    echo -e "${YELLOW}⚠️  Arquivo $ENV_FILE não encontrado - usando variáveis padrão${NC}"
fi

if [[ ! -f "Dockerfile" ]]; then
    echo -e "${RED}❌ Dockerfile não encontrado${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Arquivos necessários encontrados${NC}"

# Check if Docker is running
echo -e "${CYAN}🐳 Verificando Docker...${NC}"
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}❌ Docker não está rodando ou não está instalado${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Docker está funcionando${NC}"

# Check if Docker Compose is available
if ! command -v docker-compose > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  docker-compose não encontrado, tentando docker compose...${NC}"
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

# Show current running containers
echo -e "${CYAN}📊 Containers atuais:${NC}"
docker ps --filter "name=election" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Stop and remove old containers
echo -e "${CYAN}🛑 Parando containers antigos...${NC}"
$DOCKER_COMPOSE down --remove-orphans

# Clean up old images (optional - uncomment if you want to clean old images)
echo -e "${CYAN}🧹 Limpando imagens antigas (opcional)...${NC}"
read -p "Deseja limpar imagens Docker antigas? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}🗑️  Removendo imagens não utilizadas...${NC}"
    docker image prune -f
    echo -e "${GREEN}✅ Limpeza concluída${NC}"
else
    echo -e "${YELLOW}⏭️  Pulando limpeza de imagens${NC}"
fi

# Build new image
echo -e "${PURPLE}🔨 Construindo nova imagem da API v${NEW_VERSION}...${NC}"
$DOCKER_COMPOSE build --no-cache --build-arg BUILD_DATE="$(date -u +"%Y-%m-%dT%H:%M:%SZ")" api

# Verify the image was built
echo -e "${CYAN}🔍 Verificando imagem construída...${NC}"
if docker images | grep -q "election"; then
    echo -e "${GREEN}✅ Nova imagem construída com sucesso${NC}"
    docker images | grep "election" | head -5
else
    echo -e "${RED}❌ Falha na construção da imagem${NC}"
    exit 1
fi

# Start services
echo -e "${PURPLE}🚀 Iniciando serviços atualizados...${NC}"
$DOCKER_COMPOSE up -d

# Wait for services to be healthy
echo -e "${CYAN}⏳ Aguardando serviços ficarem saudáveis...${NC}"
timeout=120
counter=0

while [ $counter -lt $timeout ]; do
    api_health=$(docker inspect --format='{{.State.Health.Status}}' election-api-v${NEW_VERSION} 2>/dev/null || echo "no-health")
    db_health=$(docker inspect --format='{{.State.Health.Status}}' mysql-election-system-v${NEW_VERSION} 2>/dev/null || echo "no-health")
    
    if [[ "$api_health" == "healthy" && "$db_health" == "healthy" ]]; then
        echo -e "${GREEN}✅ Todos os serviços estão saudáveis!${NC}"
        break
    fi
    
    echo -e "${YELLOW}⏳ Aguardando... API: $api_health, DB: $db_health ($counter/${timeout}s)${NC}"
    sleep 5
    counter=$((counter + 5))
done

if [ $counter -ge $timeout ]; then
    echo -e "${RED}⚠️  Timeout aguardando serviços. Verificando status...${NC}"
fi

# Show final status
echo -e "${BLUE}📊 Status final dos serviços:${NC}"
$DOCKER_COMPOSE ps

# Test API endpoint
echo -e "${CYAN}🧪 Testando endpoint da API...${NC}"
sleep 10  # Wait a bit more for API to be fully ready

if curl -f -s http://localhost:5110/health > /dev/null; then
    echo -e "${GREEN}✅ API está respondendo na porta 5110${NC}"
    
    # Get API info
    api_info=$(curl -s http://localhost:5110/ || echo "Unable to get API info")
    echo -e "${CYAN}📋 Informações da API:${NC}"
    echo "$api_info" | head -10
else
    echo -e "${RED}❌ API não está respondendo. Verificando logs...${NC}"
    echo -e "${YELLOW}🔍 Logs da API:${NC}"
    $DOCKER_COMPOSE logs --tail=20 api
fi

# Show logs briefly
echo -e "${CYAN}📋 Logs recentes da API:${NC}"
$DOCKER_COMPOSE logs --tail=10 api

# Final summary
echo -e "${BLUE}===============================================${NC}"
echo -e "${GREEN}🎉 Atualização concluída!${NC}"
echo -e "${BLUE}===============================================${NC}"
echo -e "${CYAN}📊 Resumo:${NC}"
echo -e "  🔢 Versão: ${GREEN}${NEW_VERSION}${NC}"
echo -e "  🌐 URL da API: ${GREEN}http://localhost:5110${NC}"
echo -e "  🏥 Health Check: ${GREEN}http://localhost:5110/health${NC}"
echo -e "  📊 Status: ${GREEN}http://localhost:5110/health/ready${NC}"
echo -e "  📚 Documentação: ${GREEN}http://localhost:5110/swagger${NC}"
echo -e "${BLUE}===============================================${NC}"

# Useful commands
echo -e "${PURPLE}🔧 Comandos úteis:${NC}"
echo -e "  Ver logs:     ${YELLOW}$DOCKER_COMPOSE logs -f api${NC}"
echo -e "  Ver status:   ${YELLOW}$DOCKER_COMPOSE ps${NC}"
echo -e "  Parar tudo:   ${YELLOW}$DOCKER_COMPOSE down${NC}"
echo -e "  Reiniciar:    ${YELLOW}$DOCKER_COMPOSE restart${NC}"

echo -e "${GREEN}✨ Deploy concluído com sucesso!${NC}"