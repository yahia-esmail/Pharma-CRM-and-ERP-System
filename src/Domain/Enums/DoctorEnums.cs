namespace PharmaERP.Domain.Enums;

public enum DoctorStatus
{
    Prospect = 0,
    Active = 1,
    Inactive = 2
}

public enum FollowUpStatus
{
    Open = 0,
    Closed = 1
}

public enum FollowUpType
{
    PhoneCall = 0,
    Visit = 1,
    SampleDelivery = 2,
    Other = 3
}
