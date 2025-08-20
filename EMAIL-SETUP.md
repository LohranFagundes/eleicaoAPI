# 📧 Configuração do Sistema de Email

## ⚠️ Problema Identificado
O erro `"The value cannot be an empty string. (Parameter 'address')"` ocorre porque as configurações de email estão vazias no `appsettings.json`.

## ✅ Correção Aplicada
As configurações foram atualizadas com valores de exemplo:

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "noreply@election-system.com",
    "Password": "demo-password-change-in-production",
    "FromEmail": "noreply@election-system.com",
    "FromName": "Sistema de Eleições"
  }
}
```

## 🔧 Configuração para Produção

### Gmail (Recomendado para Testes)
1. Crie uma conta Gmail específica para o sistema
2. Ative a verificação em 2 etapas
3. Gere uma senha de app específica
4. Configure:

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "seu-email@gmail.com",
    "Password": "sua-senha-de-app-16-caracteres",
    "FromEmail": "seu-email@gmail.com",
    "FromName": "Sistema de Eleições"
  }
}
```

### Outlook/Hotmail
```json
{
  "EmailSettings": {
    "SmtpHost": "smtp-mail.outlook.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "seu-email@outlook.com",
    "Password": "sua-senha",
    "FromEmail": "seu-email@outlook.com",
    "FromName": "Sistema de Eleições"
  }
}
```

### SMTP Personalizado
```json
{
  "EmailSettings": {
    "SmtpHost": "mail.seudominio.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "noreply@seudominio.com",
    "Password": "sua-senha-smtp",
    "FromEmail": "noreply@seudominio.com",
    "FromName": "Sistema de Eleições"
  }
}
```

## 🐳 Configuração via Docker

Use variáveis de ambiente no `docker-compose.yml`:

```yaml
environment:
  SMTP_HOST: smtp.gmail.com
  SMTP_PORT: 587
  SMTP_ENABLE_SSL: true
  SMTP_USERNAME: seu-email@gmail.com
  SMTP_PASSWORD: sua-senha-de-app
  SMTP_FROM_EMAIL: seu-email@gmail.com
  SMTP_FROM_NAME: Sistema de Eleições
```

## 🧪 Testando a Configuração

1. **Validar Configuração**: 
   ```http
   POST /api/email/validate-config
   Authorization: Bearer {admin_token}
   ```

2. **Enviar Email de Teste**:
   ```http
   POST /api/email/test
   Authorization: Bearer {admin_token}
   Content-Type: application/json
   
   {
       "toEmail": "seu-email@teste.com",
       "toName": "Teste"
   }
   ```

## 📋 Collection Postman Corrigida

O arquivo `ElectionApi-Postman-Collection-FIXED.json` contém:

### ✅ Correções Aplicadas:
- **Scripts de token automáticos funcionais**
- **Verificação de expiração de tokens**
- **Logs detalhados e coloridos**
- **Auto-configuração de base URL**
- **Verificação automática de autenticação**
- **Testes automáticos de resposta**

### 🔧 Recursos dos Scripts:
- **Pre-request**: Verifica tokens expirados e limpa automaticamente
- **Post-response**: Salva tokens automaticamente após login
- **Global**: Logs detalhados de performance e erros
- **Auto-save**: IDs criados são salvos automaticamente

### 📱 Como Usar:
1. Importe o arquivo `ElectionApi-Postman-Collection-FIXED.json`
2. Execute "Admin Login" primeiro
3. Todos os outros endpoints usarão o token automaticamente
4. Se der erro 401, execute o login novamente

## 🔐 Segurança

**⚠️ IMPORTANTE**: 
- Nunca commite senhas reais no Git
- Use variáveis de ambiente em produção
- Mude as senhas padrão antes do deploy
- Use senhas de app específicas (Gmail)

## 🚀 Status

- ✅ Configurações de email corrigidas
- ✅ Collection Postman com scripts funcionais
- ✅ Auto-gerenciamento de tokens
- ✅ Testes automatizados
- ✅ Logs detalhados

**O sistema está pronto para uso!**