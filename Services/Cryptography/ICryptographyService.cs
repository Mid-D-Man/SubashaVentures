namespace SubashaVentures.Services.Cryptography;

public interface ICryptographyService
{
    Task<string> GenerateAesKey(int bitLength = 256);
    Task<string> GenerateIv();
    Task<string> EncryptData(string data, string key, string iv);
    Task<string> DecryptData(string encryptedData, string key, string iv);
    Task<string> HashData(string data, string algorithm = "SHA-256");
    Task<string> SignData(string data, string key, string algorithm = "SHA-256");
    Task<bool> VerifyHmac(string data, string signature, string key, string algorithm = "SHA-256");
    Task<string> GenerateCodeChallenge(string codeVerifier);
}
