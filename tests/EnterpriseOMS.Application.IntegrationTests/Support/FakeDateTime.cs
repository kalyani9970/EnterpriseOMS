using EnterpriseOMS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Support
{
    /// <summary>
    /// Controllable clock. It starts at the real current time on purpose: RefreshToken.IsActive
    /// in the Domain reads DateTime.UtcNow directly, so fake "now" values far from real time
    /// would make freshly issued tokens look expired.
    /// </summary>
    public sealed class FakeDateTime : IDateTime
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }
}