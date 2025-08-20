# 🚀 Guia de Configuração do Postman - Election API v1.1.1

## 📋 **Problema Resolvido**
✅ **Scripts de automação corrigidos** para capturar e armazenar tokens JWT corretamente  
✅ **Debug melhorado** com logs detalhados no Console  
✅ **Tratamento de 2FA** aprimorado  
✅ **Detecção automática** de erros 401 Unauthorized  

---

## 🔧 **Setup Inicial**

### 1. **Importe a Collection Atualizada**
```bash
# Arquivo atualizado:
Documentation/ElectionAPI-Postman-Collection-Complete.json
```

### 2. **Configure as Variáveis de Ambiente**
No Postman, crie um Environment com:
```
base_url = http://localhost:5110
admin_token = [deixe vazio - será preenchido automaticamente]
```

---

## 🔐 **Fluxo de Autenticação**

### **PASSO 1: Teste Básico**
Execute primeiro: **`Health Check`** para verificar se a API está rodando.

### **PASSO 2: Login Inicial**
Execute: **`Admin Login (Verificação 2FA)`**

**Cenários possíveis:**

#### 🎯 **Cenário A: Login Direto (sem 2FA)**
```json
// Resposta de sucesso:
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": { ... }
  }
}
```
✅ **Token será salvo automaticamente!**

#### 🔐 **Cenário B: 2FA Requerido**
```json
// Resposta de 2FA:
{
  "success": true,
  "data": {
    "requiresTwoFactor": true,
    "message": "Código de verificação enviado por email"
  }
}
```
➡️ **Prossiga para o PASSO 3**

### **PASSO 3: Complete o Login com 2FA**
1. **Verifique seu email** para o código de 6 dígitos
2. **Edite o body** do endpoint **`Admin Login com 2FA (Completo)`**:
   ```json
   {
     "email": "admin@election-system.com",
     "password": "admin123",
     "twoFactorToken": "123456"  // ← Cole o código aqui
   }
   ```
3. **Execute** o endpoint
4. ✅ **Token será salvo automaticamente!**

### **PASSO 4: Validação (Opcional)**
Execute: **`Validar Token`** para confirmar que o token foi salvo corretamente.

---

## 🛠️ **Debugging - Console do Postman**

### **Como Acessar:**
1. Abra o **Console** no Postman (View → Show Postman Console)
2. Execute qualquer endpoint
3. Veja logs detalhados como:

```
🔑 Token atual disponível: eyJhbGciOiJIUzI1NiIsInR5...
📡 Requisição: POST http://localhost:5110/api/auth/admin/login
📊 Status da resposta: 200 OK
✅ Token JWT salvo: eyJhbGciOiJIUzI1NiIsInR5...
```

### **Logs de Erro 401:**
```
🚨 ERRO 401 - Token inválido ou ausente!
💡 Solução: Execute o login primeiro
❌ Nenhum token encontrado - Execute: Admin Login com 2FA
```

---

## 🔄 **Scripts Automáticos Implementados**

### **Login Scripts:**
- ✅ Captura token em **3 locais**: Environment, Globals, Collection Variables
- ✅ Debug completo com logs no Console
- ✅ Validação de estrutura da resposta
- ✅ Tratamento de erros específicos

### **Global Scripts:**
- ✅ **Pre-request**: Mostra token atual e info da requisição
- ✅ **Test**: Detecta erro 401 e sugere soluções
- ✅ **Headers**: User-Agent automático

---

## 🎯 **Uso Após Login**

Depois do login bem-sucedido, **TODOS** os endpoints protegidos funcionarão automaticamente:

```bash
✅ GET /api/adminmanagement
✅ POST /api/adminmanagement  
✅ GET /api/election
✅ POST /api/candidate
# ... todos os outros endpoints
```

**Não é necessário** copiar/colar o token manualmente!

---

## 🚨 **Solução de Problemas**

### **Problema: Token não é salvo**
1. ✅ Verifique se o Environment está selecionado
2. ✅ Abra o Console para ver logs detalhados
3. ✅ Execute "Validar Token" para diagnóstico

### **Problema: 401 Unauthorized**
1. ✅ Verifique no Console se há token armazenado
2. ✅ Execute "Validar Token" para testar expiração
3. ✅ Refaça o login se necessário

### **Problema: 2FA não funciona**
1. ✅ Verifique configuração SMTP no Docker
2. ✅ Confirme que o admin tem 2FA habilitado
3. ✅ Verifique spam/lixo eletrônico

---

## 📋 **Endpoints Principais**

### **🔐 Autenticação:**
1. `Admin Login (Verificação 2FA)` - Primeiro passo
2. `Admin Login com 2FA (Completo)` - Com código 2FA
3. `Validar Token` - Testa token atual
4. `Logout` - Encerra sessão

### **👥 Admin Management:**
- `Listar Administradores` 
- `Criar Novo Administrador`
- `Atualizar Administrador`
- `Desativar Administrador`

### **🗳️ Sistema Eleitoral:**
- Eleições, Posições, Candidatos, Votação

### **🔒 Segurança:**
- System Seal, Auditoria, Relatórios

---

## ✅ **Tudo Funcionando?**

Se seguiu todos os passos e ainda tem problemas:

1. **Reimporte** a collection atualizada
2. **Recrie** o Environment
3. **Reinicie** o Docker se necessário
4. **Verifique** os logs no Console do Postman

**Collection 100% funcional para todos os endpoints da API!** 🚀