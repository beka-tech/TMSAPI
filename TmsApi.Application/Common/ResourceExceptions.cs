namespace TmsApi.Application.Common;

public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class ResourceConflictException(string message) : Exception(message);
