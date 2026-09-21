namespace ZombieShooter
{
    /// <summary>
    /// The 2D render stack, in one place. Characters are Y-sorted within their own band, which is
    /// kept clear of the floor below and the projectiles above so depth sorting can never collide
    /// with layering.
    /// </summary>
    public static class SortingBands
    {
        public const int Floor = -1000;
        public const int Wall = -900;

        /// <summary>Telegraphs, hazard pools - painted on the ground, under everything alive.</summary>
        public const int GroundDecal = -800;

        public const int Pickup = -700;

        /// <summary>Characters occupy CharacterBase -/+ (arena height * 100).</summary>
        public const int CharacterBase = 5000;
        public const int CharacterMin = 3000;
        public const int CharacterMax = 7000;

        public const int Projectile = 8000;
        public const int Fx = 8500;
        public const int DamageNumber = 9000;
    }
}
