#!/bin/bash

# =============================================================================
# 🚀 ElectionAPI v1.1.2 - Deploy Script Avançado
# =============================================================================
# Este script automatiza o deploy completo da aplicação Election API
# com verificações de saúde, logs detalhados e rollback automático
# =============================================================================

set -euo pipefail  # Exit on error, undefined vars, pipe failures

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
APP_VERSION="1.1.2"
CONTAINER_API="election-api-v1.1.2-master-user"
CONTAINER_DB="mysql-election-system-v1.1.2"
HEALTH_ENDPOINT="http://localhost:5110/health"
MAX_WAIT_TIME=180  # 3 minutos
CHECK_INTERVAL=5   # 5 segundos

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
    echo "🚀 $APP_NAME v$APP_VERSION - Deploy Script"
    echo "📅 $(date '+%Y-%m-%d %H:%M:%S BRT')"
    echo "==============================================="
    echo -e "${NC}"
}

# Função para verificar pré-requisitos
check_prerequisites() {
    log "INFO" "Verificando pré-requisitos..."
    
    # Verificar se Docker está rodando
    if ! docker info > /dev/null 2>&1; then
        log "ERROR" "Docker não está rodando. Inicie o Docker e tente novamente."
        exit 1
    fi
    
    # Verificar se docker-compose está instalado
    if ! command -v docker-compose &> /dev/null; then
        log "ERROR" "docker-compose não encontrado. Instale o docker-compose."
        exit 1
    fi
    
    # Verificar se arquivo .env existe
    if [ ! -f ".env" ]; then
        log "ERROR" "Arquivo .env não encontrado. Crie o arquivo .env com as configurações necessárias."
        exit 1
    fi
    
    # Verificar se curl está disponível para health checks
    if ! command -v curl &> /dev/null; then
        log "WARN" "curl não encontrado. Health checks podem falhar."
    fi
    
    log "SUCCESS" "Pré-requisitos verificados com sucesso"
}

# Função para fazer backup dos containers atuais
backup_containers() {
    log "INFO" "Fazendo backup dos containers atuais..."
    
    # Backup da imagem da API atual se existir
    if docker image inspect electionapinet-api:latest > /dev/null 2>&1; then
        docker tag electionapinet-api:latest electionapinet-api:backup-$(date +%s)
        log "SUCCESS" "Backup da imagem criado"
    fi
}

# Função para parar containers
stop_containers() {
    log "INFO" "Parando containers existentes..."
    
    if docker ps -q --filter "name=$CONTAINER_API" | grep -q .; then
        docker-compose stop api
        log "SUCCESS" "Container da API parado"
    fi
    
    # Aguardar containers pararem completamente
    sleep 5
}

# Função para buildar nova imagem
build_image() {
    log "INFO" "Iniciando build da nova imagem..."
    
    # Build com cache para acelerar o processo
    if ! docker-compose build api --no-cache; then
        log "ERROR" "Falha no build da imagem"
        return 1
    fi
    
    log "SUCCESS" "Imagem buildada com sucesso"
}

# Função para subir containers
start_containers() {
    log "INFO" "Iniciando containers..."
    
    # Subir database primeiro
    docker-compose up db -d
    
    # Aguardar database ficar saudável
    log "INFO" "Aguardando database ficar saudável..."
    local wait_time=0
    while [ $wait_time -lt $MAX_WAIT_TIME ]; do
        if docker-compose ps db | grep -q "healthy"; then
            log "SUCCESS" "Database está saudável"
            break
        fi
        sleep $CHECK_INTERVAL
        wait_time=$((wait_time + CHECK_INTERVAL))
        log "DEBUG" "Aguardando database... ($wait_time/${MAX_WAIT_TIME}s)"
    done
    
    if [ $wait_time -ge $MAX_WAIT_TIME ]; then
        log "ERROR" "Database não ficou saudável no tempo esperado"
        return 1
    fi
    
    # Subir API
    docker-compose up api -d
    
    log "SUCCESS" "Containers iniciados"
}

# Função para verificar saúde da API
check_api_health() {
    log "INFO" "Verificando saúde da API..."
    
    local wait_time=0
    while [ $wait_time -lt $MAX_WAIT_TIME ]; do
        if curl -f -s "$HEALTH_ENDPOINT" > /dev/null 2>&1; then
            local health_response=$(curl -s "$HEALTH_ENDPOINT")
            log "SUCCESS" "API está saudável: $health_response"
            return 0
        fi
        
        sleep $CHECK_INTERVAL
        wait_time=$((wait_time + CHECK_INTERVAL))
        log "DEBUG" "Aguardando API... ($wait_time/${MAX_WAIT_TIME}s)"
    done
    
    log "ERROR" "API não respondeu no tempo esperado"
    return 1
}

# Função para verificar logs da aplicação
check_logs() {
    log "INFO" "Verificando logs da aplicação..."
    
    # Mostrar últimas 10 linhas do log da API
    echo -e "${CYAN}--- Últimos logs da API ---${NC}"
    docker logs $CONTAINER_API --tail 10
    echo -e "${CYAN}--- Fim dos logs ---${NC}"
}

# Função para rollback em caso de falha
rollback() {
    log "WARN" "Iniciando rollback..."
    
    # Parar containers problemáticos
    docker-compose stop api
    
    # Verificar se existe backup para restaurar
    local backup_image=$(docker images electionapinet-api --format "table {{.Tag}}" | grep backup | head -1)
    if [ -n "$backup_image" ]; then
        log "INFO" "Restaurando imagem de backup: $backup_image"
        docker tag electionapinet-api:$backup_image electionapinet-api:latest
        docker-compose up api -d
        
        if check_api_health; then
            log "SUCCESS" "Rollback realizado com sucesso"
        else
            log "ERROR" "Rollback falhou. Intervenção manual necessária."
        fi
    else
        log "ERROR" "Nenhum backup encontrado para rollback"
    fi
}

# Função para limpeza de imagens antigas
cleanup_old_images() {
    log "INFO" "Limpando imagens antigas..."
    
    # Remover imagens de backup antigas (manter apenas as 3 mais recentes)
    local old_backups=$(docker images electionapinet-api --format "{{.Tag}}" | grep backup | tail -n +4)
    if [ -n "$old_backups" ]; then
        echo "$old_backups" | while read -r tag; do
            docker rmi electionapinet-api:$tag 2>/dev/null || true
            log "INFO" "Imagem antiga removida: $tag"
        done
    fi
    
    # Limpar imagens órfãs
    docker image prune -f > /dev/null 2>&1 || true
    
    log "SUCCESS" "Limpeza concluída"
}

# Função para exibir status final
show_status() {
    echo -e "${CYAN}"
    echo "==============================================="
    echo "📊 STATUS FINAL DO DEPLOY"
    echo "==============================================="
    echo -e "${NC}"
    
    # Status dos containers
    echo -e "${BLUE}🐳 Status dos Containers:${NC}"
    docker-compose ps
    echo
    
    # Informações da API
    echo -e "${BLUE}🌐 Informações da API:${NC}"
    echo "URL: http://localhost:5110"
    echo "Health Check: $HEALTH_ENDPOINT"
    echo "Swagger: http://localhost:5110/swagger"
    echo
    
    # Uso de recursos
    echo -e "${BLUE}💻 Uso de Recursos:${NC}"
    docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}" $CONTAINER_API $CONTAINER_DB 2>/dev/null || echo "Estatísticas não disponíveis"
    echo
    
    echo -e "${GREEN}✅ Deploy concluído com sucesso!${NC}"
}

# Função principal
main() {
    local start_time=$(date +%s)
    
    show_banner
    
    # Verificar se deve fazer rollback (--rollback flag)
    if [ "${1:-}" = "--rollback" ]; then
        rollback
        exit $?
    fi
    
    # Executar deploy
    {
        check_prerequisites &&
        backup_containers &&
        stop_containers &&
        build_image &&
        start_containers &&
        check_api_health &&
        cleanup_old_images &&
        show_status
    } || {
        log "ERROR" "Deploy falhou. Iniciando rollback automático..."
        rollback
        exit 1
    }
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    log "SUCCESS" "Deploy concluído em ${duration}s"
    check_logs
}

# Trap para cleanup em caso de interrupção
trap 'log "WARN" "Deploy interrompido pelo usuário"; exit 130' INT TERM

# Executar função principal
main "$@"