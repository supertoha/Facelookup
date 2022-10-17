using FaceLookup.Service.Interfaces;
using System;

namespace FaceLookup.MsSqlDataProvider
{
    public class IndexVersion : IFaceIndexInfo
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public byte[] Data { get; set; }
        public int Version { get; set; }

        public int Size { get; set; }
    }
}
