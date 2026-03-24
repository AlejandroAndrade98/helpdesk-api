namespace HelpDeskApi.Domain;

public enum TicketStatus
{
    New = 1,
    Open = 2,
    InProgress = 3,
    WaitingOnCustomer = 4,
    Resolved = 5,
    Closed = 6,
    OnHold = 7
}

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum UserRole
{
    Requester = 1,
    Agent = 2,
    Admin = 3
}