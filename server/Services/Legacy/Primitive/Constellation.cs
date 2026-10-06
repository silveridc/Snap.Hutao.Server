namespace server.Services.Legacy.Primitive
{
    /// <summary>
    /// 命座
    /// </summary>
    public readonly struct Constellation(int value) : IEquatable<Constellation>
    {
        /// <summary>
        /// value
        /// </summary>
        public readonly int Value = value;

        public static implicit operator int(Constellation value)
        {
            return value.Value;
        }

        public static implicit operator Constellation(int value)
        {
            return new(value);
        }

        public static bool operator ==(Constellation left, Constellation right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(Constellation left, Constellation right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public bool Equals(Constellation other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Constellation other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}