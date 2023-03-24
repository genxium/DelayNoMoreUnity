using backend.Storage;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("/Auth")]
public class AuthController : ControllerBase {
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthTokenCache _tokenCache;
    private readonly ICaptchaCache _captchaCache;

    public AuthController(ILogger<AuthController> logger, IAuthTokenCache tokenCache, ICaptchaCache captchaCache) {
        _logger = logger;
        _tokenCache = tokenCache;
        _captchaCache = captchaCache;
    }

    [HttpGet]
    [Route("/SmsCaptcha/Get")]
    public ActionResult GetCaptcha([FromQuery] string uname) {
        string? newCaptcha = null;
        bool res = _captchaCache.GenerateNewCaptchaForUname(uname, out newCaptcha);
        return Ok(newCaptcha);
    }

    [HttpPost]
    [Route("/SmsCaptcha/Login")]
    public ActionResult<string> Login([FromForm] string uname, [FromForm] string captcha) {
        throw new NotImplementedException();
    }
}
