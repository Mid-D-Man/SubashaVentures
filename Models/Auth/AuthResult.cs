// Models/Auth/AuthResult.cs
namespace SubashaVentures.Models.Auth;

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;

    public static AuthResult Success(string message = "Operation successful")
    {
        return new AuthResult
        {
            Success = true,
            Message = message
        };
    }

    public static AuthResult Fail(string message, string errorCode = "")
    {
        return new AuthResult
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}
