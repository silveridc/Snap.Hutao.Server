namespace server.Services.Legacy.Primitive
{
    /// <summary>
    /// 角色Id
    /// </summary>
    public readonly struct AvatarId(int value) : IEquatable<AvatarId>
    {
        /// <summary>
        /// 值
        /// </summary>
        public readonly int Value = value;

        public static implicit operator int(AvatarId value)
        {
            return value.Value;
        }

        public static implicit operator AvatarId(int value)
        {
            return new(value);
        }

        public static bool operator ==(AvatarId left, AvatarId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(AvatarId left, AvatarId right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public bool Equals(AvatarId other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is AvatarId other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}