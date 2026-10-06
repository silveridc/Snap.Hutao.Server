namespace server.Extension
{
    public static class NumberExtension
    {
        public static int StringLength(this in int number)
        {
            return (int)MathF.Log10(number) + 1;
        }
    }
}