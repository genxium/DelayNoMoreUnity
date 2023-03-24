using System;
namespace backend.Storage;

public interface ICaptchaCache {
    public bool GenerateNewCaptchaForUname(string uname, out string? captcha);
    public bool ValidateUnameCaptchaPair(string uname, string captcha);
}

