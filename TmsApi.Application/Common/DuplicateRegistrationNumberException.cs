namespace TmsApi.Application.Common;

public sealed class DuplicateRegistrationNumberException(string registrationNumber)
    : Exception($"Registration number '{registrationNumber}' is already in use.")
{
    public string RegistrationNumber { get; } = registrationNumber;
}
