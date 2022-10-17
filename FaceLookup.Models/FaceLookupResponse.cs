using System;
using System.Collections.Generic;
using System.Text;

namespace FaceLookup.Models
{
    public class FaceLookupResponse
    {
        public int Status { get; set; }
        public string StatusDescription { get; set; }

        public FaceLookupPerson[] Faces { get; set; }

    }
}
