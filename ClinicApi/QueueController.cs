using Microsoft.AspNetCore.Mvc;
using RadiantMindClinic.Models;
using RadiantMindClinic.Services;

namespace RadiantMindClinic.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly IQueueService _queueService;

        public QueueController(IQueueService queueService)
        {
            _queueService = queueService;
        }

        // GET: api/queue?serviceName=Dr. Nate
        [HttpGet]
        public IActionResult GetQueue([FromQuery] string serviceName)
        {
            var queue = _queueService.GetCurrentQueue(serviceName);
            return Ok(queue);
        }

        // POST: api/queue/join
        [HttpPost("join")]
        public IActionResult JoinQueue([FromBody] JoinQueueRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Contact))
            {
                return BadRequest(new { message = "Patient name and contact info are required." });
            }

            var item = _queueService.JoinQueue(request);
            return Ok(item);
        }

        // DELETE: api/queue/leave/{id}
        [HttpDelete("leave/{id}")]
        public IActionResult LeaveQueue(string id)
        {
            bool success = _queueService.LeaveQueue(id);
            if (!success) return NotFound(new { message = "Patient not found in queue." });

            return Ok(new { message = "Successfully removed from queue." });
        }

        // POST: api/queue/serve-next?serviceName=Dr. Nate
        [HttpPost("serve-next")]
        public IActionResult ServeNext([FromQuery] string serviceName)
        {
            var servedPatient = _queueService.ServeNext(serviceName);
            if (servedPatient == null) return NotFound(new { message = "No patients waiting in queue." });

            return Ok(servedPatient);
        }

        // GET: api/queue/status/{id}
        [HttpGet("status/{id}")]
        public IActionResult GetStatus(string id)
        {
            var status = _queueService.GetPatientStatus(id);
            if (status == null) return NotFound(new { message = "Active queue entry not found." });

            return Ok(status);
        }
    }
}
