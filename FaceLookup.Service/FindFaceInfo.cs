using FaceLookup.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaceLookup.Service
{
    public class FindFaceInfo
    {
        public IFaceIndexsItem Face { get; set; }

        public float Distance { get; set; }
    }
}
