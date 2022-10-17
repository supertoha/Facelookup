using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using FaceLookup.Service.Interfaces;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
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

            var nameGenerator = new NameGenerator();

            var files = Directory.GetFiles("d:\\datasets\\faces\\");
            var persons = files.Select(x => new Person { FaceSource = x, Name = nameGenerator.GetFullName() }).ToList();

          
            index.AddBulkFaces(persons, p => { Console.WriteLine($"{p:P}"); });

            
 
            //var faces = index.FindFaces(Image.FromFile(@"d:\\datasets\\age_gender\\1_0_0_20161219205817093.jpg") as Bitmap);
            //Console.WriteLine(faces);


            Console.ReadLine();
        }
    }
}
