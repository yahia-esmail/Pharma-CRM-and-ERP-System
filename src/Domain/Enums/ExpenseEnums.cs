namespace PharmaERP.Domain.Enums;

public enum ExpenseType
{
    Travel = 0,
    Meals = 1,
    Accommodation = 2,
    Fuel = 3,
    Other = 4,

    /// <summary>Wireframe 12 — hospitality for doctors or pharmacists.</summary>
    ClientEntertainment = 5
}

public enum ExpenseStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Reimbursed = 4
}
