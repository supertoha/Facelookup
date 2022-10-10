using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using FaceLookup.Service.Interfaces;
using System;
using System.Drawing;

namespace FaceLookup.Test
{

    class Program
    {
        static void Main(string[] args)
        {
            var sqlDataProvider = new SqlDataProvider("Server=localhost;Database=facelookup;Trusted_Connection=True;");
            sqlDataProvider.EnsureCreated();

            var index = new FacesIndex<Person>(@"c:\Users\User\image_index_model\", sqlDataProvider);
            index.Init();
            //index.AddBulkFaces(new [] { new Person { FaceSource = @"d:\\datasets\\age_gender\\10_1_0_20170109204502951.jpg", Name="Ivan" } });
            index.FindFaces(Image.FromFile(@"d:\\datasets\\age_gender\\10_1_0_20170109204502951.jpg") as Bitmap);
        }
    }
}
