using System;
using System.Collections.Generic;
using System.Text;

namespace FaceLookup.Service.Interfaces
{
    public interface IFaceIndexInfo
    {
        public byte[] Data { get; set; }
        public int Version { get; set; }
    }
}
