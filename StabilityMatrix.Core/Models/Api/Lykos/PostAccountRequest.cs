namespace StabilityMatrix.Core.Models.Api.Lykos;

public record PostAccountRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string AccountName
);
