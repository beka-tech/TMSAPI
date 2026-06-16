using System.ComponentModel.DataAnnotations;

public class PaymentOptions
{
    [Required(ErrorMessage = "GatewayUrl is required")]
    public required string GatewayUrl { get; init; }

    [Range(100, 100000, ErrorMessage = "MaxDepositBirr must be between 100 and 100000")]
    public decimal MaxDepositBirr { get; init; }
}
