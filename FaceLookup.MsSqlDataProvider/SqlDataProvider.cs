using FaceLookup.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace FaceLookup.MsSqlDataProvider
{
    public class SqlDataProvider : IFacesIndexDataProvider
    {
        public SqlDataProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        private string _connectionString;


        public IFaceIndexsItem GetFaceByIndexId(int indexId, int indexVersion)
        {
            using (var db = new FaceLookupDbContext(_connectionString))
            {
                return db.Presons.Include(x => x.Version).FirstOrDefault(x => x.FaceIndexId == indexId && x.Version.Version <= indexVersion);
            }
        }

        public int AddFaces(ICollection<IFaceIndexsItem> faces, byte[] indexBlob)
        {
            using (var db = new FaceLookupDbContext(_connectionString))
            {                
                var lastVersion = db.IndexVersions.Any() ? db.IndexVersions.Max(x => x.Version) : 0;
                var maxVersion = lastVersion + 1;

                var persons = faces.Cast<Person>();

                var version = new IndexVersion { Version = maxVersion, Date = DateTime.UtcNow, Data = indexBlob };

                foreach (var person in persons)
                    person.Version = version;

                db.Presons.AddRange(persons);
                db.IndexVersions.Add(version);
                db.SaveChanges();
                return maxVersion;
            }
        }

        public bool EnsureCreated()
        {
            using (var db = new FaceLookupDbContext(_connectionString))
            {
                if (db.Database.EnsureCreated())
                {
                    db.SaveChanges();
                    return true;
                }
            }
            return false;
        }

        public (IFaceIndexInfo indexInfo, List<float[]> latentVectors) GetLastFacesIndex()
        {
            using (var db = new FaceLookupDbContext(_connectionString))
            {
                var indexInfo = db.IndexVersions.OrderBy(x => x.Version).FirstOrDefault();
                var latentVectors = db.Presons.OrderBy(x => x.FaceIndexId).Select(x => x.FaceVector).ToList();

                return (indexInfo, latentVectors);
            }
        }
    }
}
