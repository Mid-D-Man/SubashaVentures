// Models/Auth/AuthError.cs
namespace SubashaVentures.Models.Auth;

public class AuthError
{
    public int code { get; set; }
    public string error_code { get; set; } = string.Empty;
    public string msg { get; set; } = string.Empty;
}
