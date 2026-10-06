namespace server.Models.Legacy
{
    /// <summary>
    /// 总览数据
    /// </summary>
    public class Overview
    {
        public int ScheduleId { get; set; }

        public int RecordTotal { get; set; }

        public int SpiralAbyssTotal { get; set; }

        public int SpiralAbyssFullStar { get; set; }

        public int SpiralAbyssPassed { get; set; }

        public int SpiralAbyssStarTotal { get; set; }

        public long SpiralAbyssBattleTotal { get; set; }

        public long Timestamp { get; set; }

        public double TimeTotal { get; set; }

        public double TimeAverage { get; set; }
    }
}