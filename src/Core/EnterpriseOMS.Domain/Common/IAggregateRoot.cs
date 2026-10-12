using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Common
{
    /// <summary>
    /// Marker interface for Aggregate Roots — the only entities that
    /// should be loaded/persisted directly via a Repository or DbSet
    /// from the Application layer. Child entities are reached only
    /// through their aggregate root, which enforces invariants.
    /// </summary>
    public interface IAggregateRoot
    {
    }
}
