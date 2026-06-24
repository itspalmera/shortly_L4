using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shortly.Application.DTOs
{
    /// <summary>
    /// DTO de enlace de entrada para la creación de URLs por POST
    /// </summary>
    public class UrlRequest
    {
        public string Url { get; set; } = null!;
    }
}
