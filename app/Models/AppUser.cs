using System.ComponentModel.DataAnnotations;

namespace Unbreakable.Models;

// Opdracht 4: koppelt de onveranderlijke 'sub' uit het token aan een
// gebruikersnaam. De tuples in de PDP gebruiken altijd de sub.
public class AppUser
{
    [Key]
    [StringLength(64)]
    public string Sub { get; set; }

    [Required]
    [StringLength(64)]
    public string Username { get; set; }
}
