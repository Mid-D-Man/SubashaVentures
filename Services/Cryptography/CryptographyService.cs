using Microsoft.JSInterop;

namespace SubashaVentures.Services.Cryptography;

public class CryptographyService : ICryptographyService
{
    private readonly IJSRuntime _jsRuntime;

    public CryptographyService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> GenerateAesKey(int bitLength = 256)
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.generateAesKey", 
            bitLength);
    }

    public async Task<string> GenerateIv()
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.generateIv");
    }

    public async Task<string> EncryptData(string data, string key, string iv)
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.encryptData", 
            data, key, iv);
    }

    public async Task<string> DecryptData(string encryptedData, string key, string iv)
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.decryptData", 
            encryptedData, key, iv);
    }

    public async Task<string> HashData(string data, string algorithm = "SHA-256")
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.hashData", 
            data, algorithm);
    }

    public async Task<string> SignData(string data, string key, string algorithm = "SHA-256")
    {
        return await _jsRuntime.InvokeAsync<string>(
            "cryptographyHandler.signData", 
            data, key, algorithm);
    }

    public async Task<bool> VerifyHmac(string data, string signature, string key, string algorithm = "SHA-256")
    {
        return await _jsRuntime.InvokeAsync<bool>(
            "cryptographyHandler.verifyHmac", 
            data, signature, key, algorithm);
    }

    public async Task<string> GenerateCodeChallenge(string codeVerifier)
    {
        return await _jsRuntime.InvokeAsync<string>(
            "generateCodeChallenge", 
            codeVerifier);
    }
}
