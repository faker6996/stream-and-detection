using Microsoft.AspNetCore.Mvc;
using stream_multi_cam.Models;
using stream_multi_cam.Services.BoundingBoxService;

namespace stream_multi_cam.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BoundingBoxController : ControllerBase
    {
        private readonly IBoundingBoxService _bboxService;

        public BoundingBoxController(IBoundingBoxService bboxService)
        {
            _bboxService = bboxService;
        }

        [HttpPost]
        public IActionResult Post([FromBody] BoundingBoxPayload payload)
        {
            //_bboxService.UpdateBoxes(payload.CameraId, payload.Boxes);
            return Ok();
        }
    }
}
