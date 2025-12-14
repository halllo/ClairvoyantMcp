using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class FileBasedTokenStore
{
    private readonly string _tokenDirectory;

    public FileBasedTokenStore()
    {
        // Store tokens in a .tokens directory in the current directory
        _tokenDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".tokens");

        // Ensure the directory exists
        Directory.CreateDirectory(_tokenDirectory);
    }

    public List<string> GetAccountIds()
    {
        var objectIds = new List<string>();

        if (!Directory.Exists(_tokenDirectory))
        {
            return objectIds;
        }

        var tokenFiles = Directory.GetFiles(_tokenDirectory, "*.token");
        foreach (var filePath in tokenFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            objectIds.Add(fileName);
        }

        return objectIds;
    }

    public async Task StoreAsync(string accountId, byte[] blob)
    {
        var fileName = GetTokenFileName(accountId);
        var filePath = Path.Combine(_tokenDirectory, fileName);

        var tokenData = new TokenData
        {
            AccountId = accountId,
            Blob = blob,
            Timestamp = DateTimeOffset.UtcNow
        };
        var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
        var encryptedData = ProtectData(json);

        await File.WriteAllBytesAsync(filePath, encryptedData);
    }

    public async Task<byte[]?> LoadAsync(string accountId)
    {
        var fileName = GetTokenFileName(accountId);
        var filePath = Path.Combine(_tokenDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(filePath);
            var json = UnprotectData(encryptedData);
            var tokenData = JsonSerializer.Deserialize<TokenData>(json);

            if (tokenData == null)
            {
                return null;
            }

            return tokenData.Blob;
        }
        catch
        {
            return null;
        }
    }

    private string GetTokenFileName(string accountId)
    {
        // Use object ID as filename
        return $"{accountId}.token";
    }

    private byte[] ProtectData(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);

        if (OperatingSystem.IsWindows())
        {
            // Use Windows DPAPI for encryption
            return ProtectedData.Protect(dataBytes, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            // On non-Windows platforms, store without encryption (or implement cross-platform encryption)
            // For production, consider using a cross-platform encryption library
            return dataBytes;
        }
    }

    private string UnprotectData(byte[] encryptedData)
    {
        byte[] decryptedData;

        if (OperatingSystem.IsWindows())
        {
            // Use Windows DPAPI for decryption
            decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            // On non-Windows platforms, data is not encrypted
            decryptedData = encryptedData;
        }

        return Encoding.UTF8.GetString(decryptedData);
    }

    private class TokenData
    {
        public string AccountId { get; set; } = string.Empty;
        public byte[] Blob { get; set; } = Array.Empty<byte>();
        public DateTimeOffset Timestamp { get; set; }
    }
}