using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceLookup.Models
{
    public class FaceLookupRequest
    {
        public byte[] ImageData { get; set; }

        public FaceLookupBounds ImageBounds { get; set; }
    }
}
