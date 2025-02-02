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
    [Route("SmsCaptcha/Get")]
    public JsonResult GetCaptcha([FromQuery] string uname) {
        string apiPath = "/Auth/SmsCaptcha/Get";
        _logger.LogInformation("{0}#1 [ uname={1} ]", apiPath, uname);
        string? newCaptcha = null;
        DateTimeOffset absoluteExpiryTime;
        bool res = _captchaCache.GenerateNewCaptchaForUname(uname, out newCaptcha, out absoluteExpiryTime);
        if (res) {
            _logger.LogInformation("{0}#2 [ uname={1} ]: Got [ newCaptcha={2} ]", apiPath, uname, newCaptcha);
            return Json(new AuthResult{ RetCode = ErrCode.IsTestAcc, Captcha = newCaptcha, ExpiresAt = absoluteExpiryTime.UtcTicks });
        } else {
            return Json(new AuthResult{ RetCode = ErrCode.UnknownError });
        }
    }

    [HttpPost]
    [Produces("application/json")]
    [Route("SmsCaptcha/Login")]
    public JsonResult Login([FromForm] string uname, [FromForm] string captcha) {
        string apiPath = "/Auth/SmsCaptcha/Login";
        _logger.LogInformation("{0}#1 [ uname={1}, captcha={2} ]", apiPath, uname, captcha);
        string? newAuthToken = null;
        DateTimeOffset absoluteExpiryTime;
        int playerId = shared.Battle.INVALID_DEFAULT_PLAYER_ID;
        bool res1 = _captchaCache.ValidateUnameCaptchaPair(uname, captcha, out playerId);
        bool res2 = false;
        if (res1) {
            res2 = _tokenCache.GenerateNewLoginRecord(playerId, out newAuthToken, out absoluteExpiryTime);
            if (res2) {
                _logger.LogInformation("{0}#2 [ uname={1}, captcha={2} ]: Generated newToken [ playerId={3}, newToken={4} ]", apiPath, uname, captcha, playerId, newAuthToken);
                return Json(new AuthResult { RetCode = ErrCode.Ok, PlayerId = playerId, NewAuthToken = newAuthToken, ExpiresAt = absoluteExpiryTime.UtcTicks });
            }
        }
        _logger.LogWarning("{0}#2 [ uname={1}, captcha={2} ]: Failed captcha validation ]", apiPath, uname, captcha);
        return Json(new AuthResult { RetCode = ErrCode.UnknownError });
    }

    [HttpPost]
    [Produces("application/json")]
    [Route("Token/Login")]
    public JsonResult Login([FromForm] string token, [FromForm] int playerId) {
        string apiPath = "/Auth/Token/Login";
        _logger.LogInformation("{0}#1 [ token={1}, playerId={2} ]", apiPath, token, playerId);
        var (res, uname) = _tokenCache.ValidateTokenAndRetrieveUname(token, playerId);
        if (res) {
            _logger.LogInformation("{0}#2 [ token={1}, proposedPlayerId={2} ]: Retrieved uname={3} successfully ]", apiPath, token, playerId, uname);
            return Json(new AuthResult{ RetCode = ErrCode.Ok, Uname = uname });
        } else {
            _logger.LogWarning("{0}#2 [ token={1}, proposedPlayerId={2} ]: Failed auth token validation ]", apiPath, token, playerId);
            return Json(new AuthResult{ RetCode = ErrCode.UnknownError });
        }
    }
}
