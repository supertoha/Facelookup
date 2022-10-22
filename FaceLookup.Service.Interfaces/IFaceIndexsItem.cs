using System;
using System.Collections.Generic;
using System.Text;

namespace FaceLookup.Service.Interfaces
{
    public interface IFaceIndexsItem 
    {
        public string FaceSource { get; set; }

        public float[] FaceVector { get; set; }

        public int? FaceIndexId { get; set; }

        public string Name { get; set; }
    }
}
