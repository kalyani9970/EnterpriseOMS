using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Models
{
    public class Result
    {
        public bool Succeeded { get; }
        public string[] Errors { get; }

        internal Result(bool succeeded, IEnumerable<string> errors)
        {
            Succeeded = succeeded;
            Errors = errors.ToArray();
        }

        public static Result Success() => new(true, Array.Empty<string>());
        public static Result Failure(IEnumerable<string> errors) => new(false, errors);
    }
}
