using FaceLookup.Common;
using FaceLookup.Models;
using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FaceLookup.Controllers
{
    [ApiController]
    public class FaceLookupApiController : ControllerBase
    {
        public FaceLookupApiController(FacesIndex<Person> facesIndex, IConfiguration configuration)
        {
            _facesIndex = facesIndex;
            _imagesDirectoryPath = configuration.GetSection("ImagesDirectoryPath").Value;            
        }

        private readonly string _imagesDirectoryPath;
        private readonly FacesIndex<Person> _facesIndex;

        [HttpPost, Route("api/facelookup/find")]
        public async Task<FaceLookupResponse> Find(FaceLookupRequest request)
        {
            var image = ImageHelper.BitmapFromArray(request.ImageData);
            var croppedImage = ImageHelper.Cpop(image, new Rectangle(request.ImageBounds.Left, request.ImageBounds.Top, request.ImageBounds.Width, request.ImageBounds.Height));

            var theresould = 0.15F;
            var persons = _facesIndex.FindFaces(croppedImage, 5);

            if(persons.Status != ActionStatusEnum.Success)
                return new FaceLookupResponse { Status = (int)persons.Status, StatusDescription = GetResponseStatusDescription(persons.Status) };

            var foundPresons = persons.Faces.Where(x => x.Distance < theresould).OrderByDescending(x=>x.Distance).ToList();

            if(foundPresons.Count == 0)
                return new FaceLookupResponse { Status = 100, StatusDescription = "Person not found" };

            return new FaceLookupResponse { Status = 0, StatusDescription = "OK", Faces = foundPresons.Select(x=>new FaceLookupPerson 
            {
                Distance = x.Distance,
                ImageSource = x.Face.FaceSource,
                Name = x.Face.Name
                
            }).ToArray() };
        }

        [HttpGet, Route("api/facelookup/get_image")]
        public ActionResult GetImages(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new ContentResult { Content = "Invalid Id" };

            var regex = new Regex(@"^[0-9A-Za-z_( )]*\.[a-zA-Z]+");

            if (!regex.IsMatch(id))
                return new ContentResult { Content = "Invalid Id" };

            var path = Path.Combine(this._imagesDirectoryPath, id); 

            if(!System.IO.File.Exists(path))
                return new ContentResult { Content = "Invalid Id" };

            return File(System.IO.File.ReadAllBytes(path), "image/jpg");
        }


        private string GetResponseStatusDescription(ActionStatusEnum status)
        {
            switch (status)
            {
                case ActionStatusEnum.FaceNotFound:
                    return "Incoming image has no face";
                case ActionStatusEnum.MoreThenOneFaceFound:
                    return "Incoming image has more then one face";
            }

            return "Invalid status";
        }
    }
}
