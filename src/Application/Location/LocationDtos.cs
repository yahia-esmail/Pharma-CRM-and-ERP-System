namespace PharmaERP.Application.Location;

public class LocationPingRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? TimestampUtc { get; set; }
}

public record RepresentativeLocationDto(
    int RepresentativeId,
    string RepresentativeName,
    double Latitude,
    double Longitude,
    DateTime TimestampUtc,
    string Source);
