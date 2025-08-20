using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ElectionApi.Net.Services;

public class VoteCryptographyService : IVoteCryptographyService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoteCryptographyService> _logger;
    
    // Chaves derivadas do configuration e seal hash
    private const int KeySize = 256; // AES-256
    private const int IvSize = 16;   // 128 bits IV

    public VoteCryptographyService(IConfiguration configuration, ILogger<VoteCryptographyService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> EncryptVoteDataAsync(VoteEncryptionData voteData, string electionSealHash)
    {
        try
        {
            var json = JsonSerializer.Serialize(voteData, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var key = DeriveKey(electionSealHash, "VOTE_ENCRYPTION_KEY");
            
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            
            // Salvar IV no início
            await msEncrypt.WriteAsync(aes.IV, 0, aes.IV.Length);
            
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                await swEncrypt.WriteAsync(json);
            }

            var encryptedBytes = msEncrypt.ToArray();
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt vote data");
            throw new InvalidOperationException("Erro ao criptografar dados do voto", ex);
        }
    }

    public async Task<VoteEncryptionData> DecryptVoteDataAsync(string encryptedData, string electionSealHash)
    {
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedData);
            var key = DeriveKey(electionSealHash, "VOTE_ENCRYPTION_KEY");

            using var aes = Aes.Create();
            aes.Key = key;

            // Extrair IV dos primeiros 16 bytes
            var iv = new byte[IvSize];
            Array.Copy(encryptedBytes, 0, iv, 0, IvSize);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encryptedBytes, IvSize, encryptedBytes.Length - IvSize);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            var json = await srDecrypt.ReadToEndAsync();
            var voteData = JsonSerializer.Deserialize<VoteEncryptionData>(json, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            return voteData ?? throw new InvalidOperationException("Dados do voto são nulos após descriptografia");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt vote data");
            throw new InvalidOperationException("Erro ao descriptografar dados do voto", ex);
        }
    }

    public string GenerateVoteHash(string voteId, int voterId, int candidateId, DateTime votedAt)
    {
        var data = $"{voteId}-{voterId}-{candidateId}-{votedAt:yyyy-MM-dd HH:mm:ss.fff}";
        return ComputeSha256Hash(data);
    }

    public string GenerateVoteSignature(string voteHash, string encryptedData)
    {
        var masterKey = GetMasterKey();
        var signatureData = $"{voteHash}-{encryptedData}";
        
        using var hmac = new HMACSHA256(masterKey);
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureData));
        return Convert.ToBase64String(signature);
    }

    public string GenerateCreationHash(string voteId, int voterId, int electionId, DateTime createdAt)
    {
        var data = $"{voteId}-{voterId}-{electionId}-{createdAt:yyyy-MM-dd HH:mm:ss.fff}";
        return ComputeSha256Hash(data);
    }

    public string GenerateDeviceFingerprint(string userAgent, string ipAddress, Dictionary<string, string>? additionalData = null)
    {
        var fingerprintData = new StringBuilder();
        fingerprintData.Append($"IP:{ipAddress}|");
        fingerprintData.Append($"UA:{userAgent}|");
        
        if (additionalData != null)
        {
            foreach (var kvp in additionalData.OrderBy(x => x.Key))
            {
                fingerprintData.Append($"{kvp.Key}:{kvp.Value}|");
            }
        }

        return ComputeSha256Hash(fingerprintData.ToString());
    }

    public bool ValidateVoteIntegrity(string encryptedData, string voteHash, string signature)
    {
        try
        {
            var expectedSignature = GenerateVoteSignature(voteHash, encryptedData);
            return expectedSignature == signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate vote integrity");
            return false;
        }
    }

    public async Task<string> EncryptJustificationAsync(string? justification)
    {
        if (string.IsNullOrEmpty(justification))
            return string.Empty;

        try
        {
            var key = GetJustificationKey();
            
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            
            await msEncrypt.WriteAsync(aes.IV, 0, aes.IV.Length);
            
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                await swEncrypt.WriteAsync(justification);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt justification");
            throw new InvalidOperationException("Erro ao criptografar justificativa", ex);
        }
    }

    public async Task<string?> DecryptJustificationAsync(string? encryptedJustification)
    {
        if (string.IsNullOrEmpty(encryptedJustification))
            return null;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedJustification);
            var key = GetJustificationKey();

            using var aes = Aes.Create();
            aes.Key = key;

            var iv = new byte[IvSize];
            Array.Copy(encryptedBytes, 0, iv, 0, IvSize);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encryptedBytes, IvSize, encryptedBytes.Length - IvSize);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return await srDecrypt.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt justification");
            return null;
        }
    }

    private byte[] DeriveKey(string electionSealHash, string keyPurpose)
    {
        var masterKey = GetMasterKey();
        var info = Encoding.UTF8.GetBytes($"{keyPurpose}-{electionSealHash}");
        
        // Usar PBKDF2 para derivar chave
        using var pbkdf2 = new Rfc2898DeriveBytes(masterKey, info, 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize / 8); // 32 bytes para AES-256
    }

    private byte[] GetMasterKey()
    {
        var masterKey = _configuration["VoteCryptography:MasterKey"] ?? 
                       Environment.GetEnvironmentVariable("VOTE_MASTER_KEY") ?? 
                       "default-vote-master-key-for-development-only-not-secure";
        
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(masterKey));
    }

    private byte[] GetJustificationKey()
    {
        var key = _configuration["VoteCryptography:JustificationKey"] ?? 
                 Environment.GetEnvironmentVariable("JUSTIFICATION_KEY") ?? 
                 "default-justification-key-for-development-only";
        
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }

    private string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}