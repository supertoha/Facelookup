using System;
using System.Collections.Generic;
using System.Text;

namespace FaceLookup.Service
{
    public class FindFaceReport
    {
        public FindFaceInfo[] Faces { get; set; }

        public ActionStatusEnum Status { get; set; }
    }
}
