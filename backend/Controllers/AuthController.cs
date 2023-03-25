using shared;
using backend.Storage;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("/Auth")]
public class AuthController : Controller {
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthTokenCache _tokenCache;
    private readonly ICaptchaCache _captchaCache;

    public AuthController(ILogger<AuthController> logger, IAuthTokenCache tokenCache, ICaptchaCache captchaCache) {
        _logger = logger;
        _tokenCache = tokenCache;
        _captchaCache = captchaCache;
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("/SmsCaptcha/Get")]
    public JsonResult GetCaptcha([FromQuery] string uname) {
        _logger.LogInformation("/SmsCaptcha/Get#1 [ uname={0} ]", uname);
        string? newCaptcha = null;
        DateTimeOffset? absoluteExpiryTime = null;
        bool res = _captchaCache.GenerateNewCaptchaForUname(uname, out newCaptcha, out absoluteExpiryTime);
        if (res) {
            _logger.LogInformation("/SmsCaptcha/Get#2 [ uname={0} ]: Got [ newCaptcha={1} ]", uname, newCaptcha);
            return Json(new { RetCode = ErrCode.Ok, Captcha = newCaptcha, ExpiresAt = absoluteExpiryTime });
        } else {
            return Json(new { RetCode = ErrCode.UnknownError });
        }
    }

    [HttpPost]
    [Produces("application/json")]
    [Route("/SmsCaptcha/Login")]
    public JsonResult Login([FromForm] string uname, [FromForm] string captcha) {
        _logger.LogInformation("/SmsCaptcha/Login#1 [ uname={0}, captcha={1} ]", uname, captcha);
        string? newAuthToken = null;
        DateTimeOffset? absoluteExpiryTime = null;
        int playerId = shared.Battle.INVALID_DEFAULT_PLAYER_ID;
        bool res1 = _captchaCache.ValidateUnameCaptchaPair(uname, captcha, out playerId);
        bool res2 = false;
        if (res1) {
            res2 = _tokenCache.GenerateNewLoginRecord(playerId, out newAuthToken, out absoluteExpiryTime);
        }
        if (res1 && res2) {
            _logger.LogInformation("/SmsCaptcha/Login#2 [ uname={0}, captcha={1} ]: Generated newToken [ playerId={2}, newToken={3} ]", uname, captcha, playerId, newAuthToken);
            return Json(new { RetCode = ErrCode.Ok, PlayerId=playerId, NewAuthToken = newAuthToken, ExpiresAt = absoluteExpiryTime });
        } else {
            _logger.LogWarning("/SmsCaptcha/Login#2 [ uname={0}, captcha={1} ]: Failed captcha validation ]", uname, captcha);
            return Json(new { RetCode = ErrCode.UnknownError });
        }
    }

    [HttpPost]
    [Produces("application/json")]
    [Route("/AuthToken/Login")]
    public JsonResult Login([FromForm] string token, [FromForm] int playerId) {
        _logger.LogInformation("/AuthToken/Login#1 [ token={0}, playerId={1} ]", token, playerId);
        bool res = _tokenCache.ValidateToken(token, playerId);
        if (res) {
            _logger.LogInformation("/AuthToken/Login#2 [ token={0}, proposedPlayerId={1} ]: Validated successfully ]", token, playerId);
            return Json(new { RetCode = ErrCode.Ok });
        } else {
            _logger.LogWarning("/AuthToken/Login#2 [ token={0}, proposedPlayerId={1} ]: Failed auth token validation ]", token, playerId);
            return Json(new { RetCode = ErrCode.UnknownError });
        }
    }
}
