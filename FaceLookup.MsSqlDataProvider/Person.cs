using FaceLookup.Service.Interfaces;

namespace FaceLookup.MsSqlDataProvider
{
    public class Person : IFaceIndexsItem
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public float[] FaceVector { get; set; }

        public string FaceSource { get; set; }

        public int? FaceIndexId { get; set; }

        public IndexVersion Version { get; set; }
    }
}
