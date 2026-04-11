using Microsoft.AspNetCore.Mvc;
using TifSnippetApp.Client.Models;
using TifSnippetApp.Services;

namespace TifSnippetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SnippetController : ControllerBase
    {
        private readonly SnippetService _snippetService;

        public SnippetController(SnippetService snippetService)
        {
            _snippetService = snippetService;
        }

        [HttpGet("{index}")]
        public async Task<ActionResult<SnippetInfo>> GetSnippet(int index)
        {
            var result = await _snippetService.GetSnippetAsync(index);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet("image/{index}")]
        public async Task<ActionResult<string>> GetSnippetImage(int index, [FromQuery] bool expanded = false)
        {
            var result = await _snippetService.GetSnippetImageAsync(index, expanded);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet("batch")]
        public async Task<ActionResult<List<SnippetInfo>>> GetBatch([FromQuery] int start, [FromQuery] int count = 5)
        {
            var result = await _snippetService.GetSnippetsAsync(start, count);
            return Ok(result);
        }

        [HttpPost("save")]
        public async Task<ActionResult> SaveSnippet(SnippetSubmission submission)
        {
            await _snippetService.SaveResultAsync(submission);
            return Ok();
        }
    }
}
