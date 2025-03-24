namespace backend.Storage ;
public class CaptchaCacheEntry {
    public string Captcha;
    public string PlayerId;

    public CaptchaCacheEntry(string captcha, string playerId) {
        Captcha = captcha;
        PlayerId = playerId;
    }

}
