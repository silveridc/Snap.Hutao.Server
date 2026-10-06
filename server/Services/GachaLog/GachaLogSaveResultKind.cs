namespace server.Services.GachaLog
{
    public enum GachaLogSaveResultKind
    {
        Ok,
        UidPerUserLimitExceeded,
        InvalidGachaItemDetected,
        DatebaseOperationFailed,
    }
}