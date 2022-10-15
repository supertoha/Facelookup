using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using FaceLookup.Service.Interfaces;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace FaceLookup.Test
{

    class Program
    {
        static void Main(string[] args)
        {
            var sqlDataProvider = new SqlDataProvider("Server=localhost;Database=facelookup;Trusted_Connection=True;");
            sqlDataProvider.EnsureCreated();

            var index = new FacesIndex<Person>(@"c:\Users\User\image_index_model\model.onnx", sqlDataProvider);
            index.Init();
            index.AddBulkFaces(new[] { 
                new Person { FaceSource = @"d:\\datasets\\age_gender\\1_0_0_20161219205817093.jpg", Name = "Petr" },
                new Person { FaceSource = @"d:\\datasets\\age_gender\\45_0_0_20170116235729518.jpg", Name = "Oleg" } });
 
            var faces = index.FindFaces(Image.FromFile(@"d:\\datasets\\age_gender\\1_0_0_20161219205817093.jpg") as Bitmap);
            Console.WriteLine(faces);


            Console.ReadLine();
        }
    }
}
