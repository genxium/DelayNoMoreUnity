using backend.Storage;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("/Auth")]
public class AuthController : ControllerBase {
    private readonly ILogger<AuthController> _logger;
    private readonly ISimpleCache _tokenCache;

    
    public AuthController(ILogger<AuthController> logger, ISimpleCache tokenCache) {
        _logger = logger;
        _tokenCache = tokenCache;
    }

    [HttpPost]
    [Route("/SmsCaptcha/Get")]
    public async Task<ActionResult<string>> GetCaptcha([FromForm] string uname) {
        return Ok();
    }
}
