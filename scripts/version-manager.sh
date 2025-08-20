#!/bin/bash

# =============================================================================
# 🏷️ ElectionAPI - Docker Image Version Manager
# =============================================================================
# Gerencia versionamento de imagens Docker com tags semânticas e cleanup
# automático de versões antigas
# =============================================================================

set -euo pipefail

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configurações
APP_NAME="ElectionAPI"
IMAGE_NAME="electionapinet-api"
REGISTRY="${DOCKER_REGISTRY:-}"
MAX_KEEP_VERSIONS=5
CURRENT_VERSION=$(cat VERSION 2>/dev/null || echo "1.1.2")

# Função para logging com timestamp
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
        "DEBUG") echo -e "${PURPLE}[$timestamp] [DEBUG]${NC} $message" ;;
    esac
}

# Função para exibir banner
show_banner() {
    echo -e "${CYAN}"
    echo "==============================================="
    echo "🏷️  $APP_NAME - Version Manager"
    echo "📅 $(date '+%Y-%m-%d %H:%M:%S BRT')"
    echo "==============================================="
    echo -e "${NC}"
}

# Função para validar formato de versão (semver)
validate_version() {
    local version=$1
    if [[ ! $version =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9]+)?$ ]]; then
        log "ERROR" "Formato de versão inválido: $version. Use formato semver (ex: 1.2.3 ou 1.2.3-beta)"
        return 1
    fi
}

# Função para obter próxima versão
get_next_version() {
    local current=$1
    local bump_type=$2
    
    # Extrair major, minor, patch
    local major=$(echo $current | cut -d. -f1)
    local minor=$(echo $current | cut -d. -f2)
    local patch=$(echo $current | cut -d. -f3 | cut -d- -f1)
    
    case $bump_type in
        "major")
            major=$((major + 1))
            minor=0
            patch=0
            ;;
        "minor")
            minor=$((minor + 1))
            patch=0
            ;;
        "patch")
            patch=$((patch + 1))
            ;;
        *)
            log "ERROR" "Tipo de bump inválido: $bump_type. Use: major, minor, patch"
            return 1
            ;;
    esac
    
    echo "${major}.${minor}.${patch}"
}

# Função para criar tags da imagem
tag_image() {
    local version=$1
    local build_date=$(date '+%Y-%m-%d')
    local git_commit=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
    local base_image="$IMAGE_NAME:latest"
    
    log "INFO" "Criando tags para versão $version..."
    
    # Tags principais
    local tags=(
        "$IMAGE_NAME:$version"
        "$IMAGE_NAME:v$version"
        "$IMAGE_NAME:latest"
    )
    
    # Tag com data de build
    tags+=("$IMAGE_NAME:$version-$build_date")
    
    # Tag com commit (se disponível)
    if [ "$git_commit" != "unknown" ]; then
        tags+=("$IMAGE_NAME:$version-$git_commit")
    fi
    
    # Aplicar tags
    for tag in "${tags[@]}"; do
        if docker tag "$base_image" "$tag"; then
            log "SUCCESS" "Tag criada: $tag"
        else
            log "ERROR" "Falha ao criar tag: $tag"
            return 1
        fi
    done
    
    # Tags para registry (se configurado)
    if [ -n "$REGISTRY" ]; then
        log "INFO" "Criando tags para registry: $REGISTRY"
        for tag in "${tags[@]}"; do
            local registry_tag="$REGISTRY/$tag"
            if docker tag "$base_image" "$registry_tag"; then
                log "SUCCESS" "Registry tag criada: $registry_tag"
            else
                log "WARN" "Falha ao criar registry tag: $registry_tag"
            fi
        done
    fi
}

# Função para fazer push das imagens
push_images() {
    local version=$1
    
    if [ -z "$REGISTRY" ]; then
        log "WARN" "Registry não configurado. Use DOCKER_REGISTRY env var para push automático"
        return 0
    fi
    
    log "INFO" "Fazendo push das imagens para registry..."
    
    local tags=(
        "$REGISTRY/$IMAGE_NAME:$version"
        "$REGISTRY/$IMAGE_NAME:v$version"
        "$REGISTRY/$IMAGE_NAME:latest"
    )
    
    for tag in "${tags[@]}"; do
        log "INFO" "Pushing $tag..."
        if docker push "$tag"; then
            log "SUCCESS" "Push realizado: $tag"
        else
            log "ERROR" "Falha no push: $tag"
            return 1
        fi
    done
}

# Função para listar versões disponíveis
list_versions() {
    log "INFO" "Versões disponíveis localmente:"
    echo -e "${CYAN}--- Imagens Locais ---${NC}"
    docker images "$IMAGE_NAME" --format "table {{.Tag}}\t{{.CreatedAt}}\t{{.Size}}" | head -20
    echo
    
    if [ -n "$REGISTRY" ]; then
        log "INFO" "Verificando versões no registry..."
        # Aqui você pode adicionar lógica para listar tags do registry
        # Por exemplo, usando docker registry API ou skopeo
    fi
}

# Função para limpar versões antigas
cleanup_old_versions() {
    log "INFO" "Limpando versões antigas (mantendo $MAX_KEEP_VERSIONS mais recentes)..."
    
    # Obter tags ordenadas por data de criação (mais recentes primeiro)
    local old_images=$(docker images "$IMAGE_NAME" --format "{{.Tag}}" | \
        grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | \
        sort -V -r | \
        tail -n +$((MAX_KEEP_VERSIONS + 1)))
    
    if [ -n "$old_images" ]; then
        echo "$old_images" | while read -r tag; do
            log "INFO" "Removendo versão antiga: $IMAGE_NAME:$tag"
            docker rmi "$IMAGE_NAME:$tag" 2>/dev/null || true
        done
        
        # Limpar imagens órfãs
        docker image prune -f > /dev/null 2>&1 || true
        log "SUCCESS" "Limpeza concluída"
    else
        log "INFO" "Nenhuma versão antiga para remover"
    fi
}

# Função para atualizar arquivo VERSION
update_version_file() {
    local version=$1
    echo "$version" > VERSION
    log "SUCCESS" "Arquivo VERSION atualizado para $version"
}

# Função para criar release notes
create_release_notes() {
    local version=$1
    local notes_file="releases/RELEASE-$version.md"
    
    mkdir -p releases
    
    cat > "$notes_file" << EOF
# Election API .NET - Release $version

**Data de Release:** $(date '+%Y-%m-%d %H:%M:%S BRT')
**Git Commit:** $(git rev-parse --short HEAD 2>/dev/null || echo "unknown")

## Mudanças nesta versão

### Novas Features
- [ ] Adicione suas novas features aqui

### Melhorias
- [ ] Adicione suas melhorias aqui

### Bug Fixes
- [ ] Adicione seus bug fixes aqui

### Mudanças Técnicas
- [ ] Adicione mudanças técnicas aqui

## Deploy

\`\`\`bash
# Pull da nova versão
docker pull $IMAGE_NAME:$version

# Deploy usando docker-compose
APP_VERSION=$version docker-compose -f docker-compose.prod.yml up -d

# Ou usando o script de deploy
./scripts/deploy.sh
\`\`\`

## Rollback

\`\`\`bash
# Em caso de problemas
./scripts/deploy.sh --rollback
\`\`\`

---
**Testado em:** Ambiente de desenvolvimento
**Aprovado por:** [Nome do responsável]
EOF

    log "SUCCESS" "Release notes criadas: $notes_file"
}

# Função para mostrar ajuda
show_help() {
    echo -e "${CYAN}Usage: $0 [COMMAND] [OPTIONS]${NC}"
    echo
    echo "Commands:"
    echo "  bump <major|minor|patch>  - Incrementa versão e cria tags"
    echo "  tag <version>             - Cria tags para versão específica"
    echo "  push <version>            - Faz push da versão para registry"
    echo "  list                      - Lista versões disponíveis"
    echo "  cleanup                   - Remove versões antigas"
    echo "  current                   - Mostra versão atual"
    echo
    echo "Options:"
    echo "  --registry <url>          - Define registry Docker"
    echo "  --keep <number>           - Número de versões a manter (default: $MAX_KEEP_VERSIONS)"
    echo "  --help                    - Mostra esta ajuda"
    echo
    echo "Environment Variables:"
    echo "  DOCKER_REGISTRY           - Registry Docker para push automático"
    echo
    echo "Examples:"
    echo "  $0 bump patch             - Incrementa patch version (1.1.2 -> 1.1.3)"
    echo "  $0 tag 1.2.0              - Cria tags para versão 1.2.0"
    echo "  $0 push 1.2.0             - Faz push da versão 1.2.0"
    echo "  $0 cleanup                - Remove versões antigas"
}

# Função principal
main() {
    local command="${1:-help}"
    
    # Parse options
    while [[ $# -gt 0 ]]; do
        case $1 in
            --registry)
                REGISTRY="$2"
                shift 2
                ;;
            --keep)
                MAX_KEEP_VERSIONS="$2"
                shift 2
                ;;
            --help)
                show_help
                exit 0
                ;;
            *)
                break
                ;;
        esac
    done
    
    show_banner
    
    case $command in
        "bump")
            local bump_type="${2:-patch}"
            local new_version=$(get_next_version "$CURRENT_VERSION" "$bump_type")
            validate_version "$new_version"
            
            log "INFO" "Incrementando versão: $CURRENT_VERSION -> $new_version"
            update_version_file "$new_version"
            tag_image "$new_version"
            create_release_notes "$new_version"
            
            if [ -n "$REGISTRY" ]; then
                read -p "Fazer push para registry? (y/N): " -n 1 -r
                echo
                if [[ $REPLY =~ ^[Yy]$ ]]; then
                    push_images "$new_version"
                fi
            fi
            
            log "SUCCESS" "Versão bumped para $new_version"
            ;;
            
        "tag")
            local version="${2:-$CURRENT_VERSION}"
            validate_version "$version"
            tag_image "$version"
            ;;
            
        "push")
            local version="${2:-$CURRENT_VERSION}"
            validate_version "$version"
            push_images "$version"
            ;;
            
        "list")
            list_versions
            ;;
            
        "cleanup")
            cleanup_old_versions
            ;;
            
        "current")
            log "INFO" "Versão atual: $CURRENT_VERSION"
            ;;
            
        "help"|*)
            show_help
            ;;
    esac
}

# Executar função principal
main "$@"