namespace PharmaERP.Application.Common;

public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} ({key}) was not found.");

public class ForbiddenAccessException(string message = "You do not have access to this resource.")
    : Exception(message);

public class ValidationFailedException(string message) : Exception(message);
