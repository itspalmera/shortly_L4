using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Shortly.Application.DTOs
{
    [XmlRoot("Stats")]
    public class StatsResponse
    {
        public int TotalLinks { get; init; }
        public int TotalClicks { get; init; }
    }
}