using System.ComponentModel.DataAnnotations;

namespace Unbreakable.Models;

public class Message
{
    public int Id { get; set; }

    [StringLength(40)]
    [Display(Name = "Van")]
    public string Sender { get; set; }

    [StringLength(40)]
    [Display(Name = "Aan")]
    public string Recipient { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Onderwerp")]
    public string Subject { get; set; }

    // Opdracht 3: Body bevat enkel nog CIPHERTEXT (modulo-37).
    [Required]
    [StringLength(2000)]
    [Display(Name = "Bericht (versleuteld)")]
    public string Body { get; set; }

    [Required]
    [Display(Name = "Lijn uit codeboek")]
    public int KeyLine { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "MAC")]
    public string Mac { get; set; }

    // Opdracht 4: de identiteit uit het token. Sender/Recipient blijven enkel
    // de leesbare naam voor de interface; autorisatie draait op de sub.
    [StringLength(64)]
    public string SenderSub { get; set; }

    [StringLength(64)]
    public string RecipientSub { get; set; }

    [Display(Name = "Verzonden")]
    public DateTime SentAt { get; set; }
}
