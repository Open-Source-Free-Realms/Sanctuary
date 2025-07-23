namespace Sanctuary.WebAPI.Models;

public class LoginRequestModel
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}