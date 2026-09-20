namespace PharmaERP.Application.Files;

public record FileAttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);
