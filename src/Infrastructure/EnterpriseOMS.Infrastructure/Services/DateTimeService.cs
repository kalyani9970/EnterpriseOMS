using EnterpriseOMS.Application.Common.Interfaces;

namespace EnterpriseOMS.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
