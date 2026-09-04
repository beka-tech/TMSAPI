namespace TmsApi.Application.Common;

public sealed class EnrollmentRejectedException(EnrollmentError error) : Exception(error.Message)
{
    public EnrollmentError Error { get; } = error;
}
