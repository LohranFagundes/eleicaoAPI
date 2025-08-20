#!/bin/bash

# Quick Docker Update Script - Sem perguntas
# Para desenvolvimento rápido

set -euo pipefail

echo "🚀 Quick Update - Election API v1.3.0"
echo "======================================"

# Stop containers
echo "⏹️  Parando containers..."
docker-compose down

# Build and start
echo "🔨 Rebuild e start..."
docker-compose up -d --build

# Wait a moment
echo "⏳ Aguardando 15 segundos..."
sleep 15

# Test
echo "🧪 Testando API..."
if curl -f -s http://localhost:5110/health > /dev/null; then
    echo "✅ API online: http://localhost:5110"
else
    echo "❌ API com problema. Logs:"
    docker-compose logs --tail=10 api
fi

echo "✨ Quick update concluído!"