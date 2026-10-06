namespace server.Core
{
    public static class RandomHelper
    {
        public static string GetUpperAndNumberString(int count)
        {
            return new(Random.Shared.GetItems<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890", count));
        }
    }
}