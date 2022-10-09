using FaceLookup.Service;
using FaceLookup.Service.Interfaces;
using System;

namespace FaceLookup.Test
{
    //public class FaceInfo : IFaceInfo
    //{
    //    public string FaceSource { get; set; }
    //}

    public class FaceIndexItem : /*FaceInfo,*/ IFaceIndexsItem
    {
        public float[] FaceVector { get; set; }
        public int? FaceIndexId { get; set; }
        public string FaceSource { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {

            var index = new FacesIndex<FaceIndexItem>(@"c:\Users\User\image_index_model\", null);
            index.Init();
            index.AddBulkFaces(new [] { new FaceIndexItem { FaceSource = @"d:\\datasets\\age_gender\\10_1_0_20170109204502951.jpg" } });
        }
    }
}
