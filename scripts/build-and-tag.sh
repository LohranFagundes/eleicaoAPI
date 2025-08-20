#!/bin/bash

# =============================================================================
# 🏗️ Election API - Build and Tag Script
# =============================================================================
# Script integrado para build otimizado com versionamento automático
# =============================================================================

set -euo pipefail

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configurações
APP_VERSION=$(cat VERSION 2>/dev/null || echo "1.1.2")
BUILD_DATE=$(date '+%Y-%m-%d')
BUILD_TIMESTAMP=$(date '+%Y%m%d-%H%M%S')
GIT_COMMIT=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
IMAGE_NAME="electionapinet-api"

# Função para logging
log() {
    local level=$1
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S BRT')
    
    case $level in
        "INFO")  echo -e "${BLUE}[$timestamp] [INFO]${NC} $message" ;;
        "WARN")  echo -e "${YELLOW}[$timestamp] [WARN]${NC} $message" ;;
        "ERROR") echo -e "${RED}[$timestamp] [ERROR]${NC} $message" ;;
        "SUCCESS") echo -e "${GREEN}[$timestamp] [SUCCESS]${NC} $message" ;;
    esac
}

# Banner
show_banner() {
    echo -e "${CYAN}"
    echo "==============================================="
    echo "🏗️  Election API - Build & Tag"
    echo "📦 Version: $APP_VERSION"
    echo "📅 Build: $BUILD_DATE"
    echo "🔨 Commit: $GIT_COMMIT"
    echo "==============================================="
    echo -e "${NC}"
}

# Build da imagem com otimizações
build_image() {
    log "INFO" "Iniciando build otimizado..."
    
    # Build com argumentos otimizados
    if docker build \
        --build-arg APP_VERSION="$APP_VERSION" \
        --build-arg BUILD_DATE="$BUILD_DATE" \
        --build-arg BUILD_CONFIGURATION="Release" \
        --target runtime \
        --tag "$IMAGE_NAME:latest" \
        --tag "$IMAGE_NAME:$APP_VERSION" \
        --tag "$IMAGE_NAME:$APP_VERSION-$BUILD_TIMESTAMP" \
        --label "version=$APP_VERSION" \
        --label "build-date=$BUILD_DATE" \
        --label "git-commit=$GIT_COMMIT" \
        --label "maintainer=Election API Team" \
        .; then
        
        log "SUCCESS" "Build concluído com sucesso"
        
        # Mostrar informações da imagem
        echo -e "${CYAN}--- Informações da Imagem ---${NC}"
        docker image inspect "$IMAGE_NAME:$APP_VERSION" --format='
Size: {{.Size | printf "%.2f MB" | div 1000000}}
Created: {{.Created}}
Architecture: {{.Architecture}}
OS: {{.Os}}
Labels: {{json .Config.Labels}}'
        echo
        
        return 0
    else
        log "ERROR" "Build falhou"
        return 1
    fi
}

# Criar tags adicionais
create_additional_tags() {
    log "INFO" "Criando tags adicionais..."
    
    # Tag semântica
    local major=$(echo $APP_VERSION | cut -d. -f1)
    local minor=$(echo $APP_VERSION | cut -d. -f2)
    
    # Tags adicionais
    docker tag "$IMAGE_NAME:$APP_VERSION" "$IMAGE_NAME:v$APP_VERSION"
    docker tag "$IMAGE_NAME:$APP_VERSION" "$IMAGE_NAME:$major.$minor"
    docker tag "$IMAGE_NAME:$APP_VERSION" "$IMAGE_NAME:$major"
    
    # Tag com commit se disponível
    if [ "$GIT_COMMIT" != "unknown" ]; then
        docker tag "$IMAGE_NAME:$APP_VERSION" "$IMAGE_NAME:$APP_VERSION-$GIT_COMMIT"
    fi
    
    log "SUCCESS" "Tags adicionais criadas"
}

# Verificar qualidade da imagem
verify_image() {
    log "INFO" "Verificando qualidade da imagem..."
    
    # Verificar se a imagem foi criada
    if ! docker image inspect "$IMAGE_NAME:$APP_VERSION" > /dev/null 2>&1; then
        log "ERROR" "Imagem não encontrada após build"
        return 1
    fi
    
    # Verificar tamanho da imagem
    local image_size=$(docker image inspect "$IMAGE_NAME:$APP_VERSION" --format='{{.Size}}')
    local size_mb=$((image_size / 1000000))
    
    if [ $size_mb -gt 1000 ]; then
        log "WARN" "Imagem muito grande: ${size_mb}MB (considere otimizações)"
    else
        log "SUCCESS" "Tamanho da imagem OK: ${size_mb}MB"
    fi
    
    # Verificar vulnerabilidades (se trivy estiver disponível)
    if command -v trivy &> /dev/null; then
        log "INFO" "Executando scan de segurança..."
        if trivy image --exit-code 1 --severity HIGH,CRITICAL "$IMAGE_NAME:$APP_VERSION"; then
            log "SUCCESS" "Scan de segurança passou"
        else
            log "WARN" "Vulnerabilidades encontradas no scan de segurança"
        fi
    fi
    
    return 0
}

# Listar todas as tags criadas
list_created_tags() {
    echo -e "${CYAN}--- Tags Criadas ---${NC}"
    docker images "$IMAGE_NAME" --format "table {{.Tag}}\t{{.CreatedAt}}\t{{.Size}}"
    echo
}

# Função principal
main() {
    local build_only=false
    local verify_only=false
    
    # Parse argumentos
    while [[ $# -gt 0 ]]; do
        case $1 in
            --build-only)
                build_only=true
                shift
                ;;
            --verify-only)
                verify_only=true
                shift
                ;;
            --version)
                APP_VERSION="$2"
                echo "$APP_VERSION" > VERSION
                shift 2
                ;;
            --help)
                echo "Usage: $0 [options]"
                echo "Options:"
                echo "  --build-only    Apenas faz build, sem tags adicionais"
                echo "  --verify-only   Apenas verifica imagem existente"
                echo "  --version VER   Define versão específica"
                echo "  --help          Mostra esta ajuda"
                exit 0
                ;;
            *)
                log "ERROR" "Argumento desconhecido: $1"
                exit 1
                ;;
        esac
    done
    
    show_banner
    
    local start_time=$(date +%s)
    
    if [ "$verify_only" = true ]; then
        verify_image
        exit $?
    fi
    
    # Executar build
    if build_image; then
        if [ "$build_only" = false ]; then
            create_additional_tags
        fi
        
        verify_image
        list_created_tags
        
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        
        log "SUCCESS" "Build e tag concluídos em ${duration}s"
        
        # Sugerir próximos passos
        echo -e "${CYAN}--- Próximos Passos ---${NC}"
        echo "1. Testar a imagem: docker run --rm -p 5110:5110 $IMAGE_NAME:$APP_VERSION"
        echo "2. Deploy local: ./scripts/deploy.sh"
        echo "3. Push para registry: ./scripts/version-manager.sh push $APP_VERSION"
        echo "4. Deploy produção: APP_VERSION=$APP_VERSION docker-compose -f docker-compose.prod.yml up -d"
        
    else
        log "ERROR" "Build falhou"
        exit 1
    fi
}

# Trap para cleanup
trap 'log "WARN" "Build interrompido"; exit 130' INT TERM

# Executar função principal
main "$@"