using FaceLookup.Common;
using FaceLookup.Models;
using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace FaceLookup.Controllers
{
    [ApiController]
    public class FaceLookupApiController : ControllerBase
    {
        public FaceLookupApiController(FacesIndex<Person> facesIndex)
        {
            _facesIndex = facesIndex;
        }

        private readonly FacesIndex<Person> _facesIndex;

        [HttpPost, Route("api/facelookup/find")]
        public async Task<bool> Find(FaceLookupRequest request)
        {
            var image = ImageHelper.BitmapFromArray(request.ImageData);
            var croppedImage = ImageHelper.Cpop(image, new Rectangle(request.ImageBounds.Left, request.ImageBounds.Top, request.ImageBounds.Width, request.ImageBounds.Height));

            var persons = _facesIndex.FindFaces(croppedImage, 5);
            Console.WriteLine(persons);
            return false;
        }
    }
}
