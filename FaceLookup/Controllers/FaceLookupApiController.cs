using FaceLookup.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FaceLookup.Controllers
{
    [ApiController]
    public class FaceLookupApiController : ControllerBase
    {
        [HttpPost, Route("api/facelookup/find")]
        public async Task<bool> Find(FaceLookupRequest request)
        {
            return false;

        }
    }
}
