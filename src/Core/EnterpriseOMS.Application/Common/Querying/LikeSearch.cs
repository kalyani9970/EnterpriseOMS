using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Querying
{
    /// <summary>
    /// Builds a safe "contains" pattern for SQL LIKE. A user who types "50%" or "a_b" means those literal
    /// characters, not wildcards, so %, _, [ and \ are escaped. Use it together with
    /// EF.Functions.Like(column, pattern, LikeSearch.EscapeCharacter).
    /// LIKE is case-insensitive on SQL Server (default collation) and on SQLite.
    /// </summary>
    public static class LikeSearch
    {
        public const string EscapeCharacter = "\\";

        public static string ContainsPattern(string text) => $"%{Escape(text.Trim())}%";

        public static string Escape(string text) => text
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
    }
}
