using System.ComponentModel.DataAnnotations;

namespace Sanctuary.Core.Configuration;

public class ServerOptions
{
    public const string Section = "Server";

    [Required]
    public required int Port { get; set; }

    public required bool UseCompression { get; set; }
}