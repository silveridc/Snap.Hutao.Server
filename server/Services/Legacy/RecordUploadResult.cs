namespace server.Services.Legacy
{
    public enum RecordUploadResult
    {
        OkWithNotFirstAttempt,
        OkWithNoUserNamePresented,
        OkWithGachaLogExtended,
        OkWithGachaLogNoSuchUser,
        None,
        ComputingStatistics,
        UidBanned,
        NotCurrentSchedule,
        InvalidData,
        ConcurrencyNotSupported,
    }
}