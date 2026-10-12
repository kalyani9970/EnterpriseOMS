using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Mappings
{
    /// <summary>
    /// DTOs implement this to declare "I map from TSource". Combined with
    /// MappingProfile's reflection scan, this avoids hand-writing a
    /// CreateMap<,>() line for every simple DTO — only override Mapping()
    /// when the mapping isn't a straight property match.
    /// </summary>
    public interface IMapFrom<T>
    {
        void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
    }
}
