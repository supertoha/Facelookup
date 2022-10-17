using System.Collections.Generic;

namespace FaceLookup.Service.Interfaces
{
    public interface IFacesIndexDataProvider
    {
        public IFaceIndexsItem GetFaceByIndexId(int indexId, int indexVersion);
        public int AddFaces(ICollection<IFaceIndexsItem> faces, byte[] indexBlob, int personsNumber);
        public bool EnsureCreated();
        public (IFaceIndexInfo indexInfo, List<float[]> latentVectors) GetLastFacesIndex();
    }
}
