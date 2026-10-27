using System.ComponentModel.DataAnnotations;

namespace Unbreakable.Models;

public class Message
{
    public int Id { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 2)]
    [Display(Name = "Van")]
    public string Sender { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 2)]
    [Display(Name = "Aan")]
    public string Recipient { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Onderwerp")]
    public string Subject { get; set; }

    // Opdracht 3: Body bevat enkel nog CIPHERTEXT (modulo-37). Nooit de plain-text, nooit de sleutel.
    [Required]
    [StringLength(2000)]
    [Display(Name = "Bericht (versleuteld)")]
    public string Body { get; set; }

    // Opdracht 3: lijnnummer uit het codeboek dat als sleutel gebruikt werd.
    [Required]
    [Display(Name = "Lijn uit codeboek")]
    public int KeyLine { get; set; }

    // Opdracht 3: HMAC-SHA256 over lijnnummer + ciphertext (hex). Voor integriteit.
    [Required]
    [StringLength(64)]
    [Display(Name = "MAC")]
    public string Mac { get; set; }

    [Display(Name = "Verzonden")]
    public DateTime SentAt { get; set; }
}
