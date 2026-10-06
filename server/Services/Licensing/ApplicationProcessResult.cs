namespace server.Services.Licensing
{
    public enum ApplicationProcessResult
    {
        Ok,
        ReCaptchaVerificationFailed,
        UsetNotExists,
        AlreadyApplied,
    }
}