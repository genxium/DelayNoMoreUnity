using System;
namespace backend.Storage;

public interface ICaptchaCache {
    public bool GenerateNewCaptchaForUname(string uname, out string? newCaptcha, out DateTimeOffset absoluteExpiryTime);
    public bool ValidateUnameCaptchaPair(string uname, string captcha, out int playerId);
}

