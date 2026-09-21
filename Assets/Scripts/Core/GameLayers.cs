using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Layer indices and the collision rules between them. Configured in code rather than the
    /// Physics 2D matrix so the rules live next to the reason for them and survive a settings reset.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Projectile = 10;
        public const int Pickup = 11;
        public const int EnemyProjectile = 12;

        public static int EnemyMask { get { return 1 << Enemy; } }
        public static int PlayerMask { get { return 1 << Player; } }

        /// <summary>
        /// Runs before the first scene loads. Projectiles only care about enemies; pickups are
        /// collected by proximity, not physics; enemies push each other but nothing else.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ConfigureCollisionMatrix()
        {
            Physics2D.IgnoreLayerCollision(Projectile, Projectile, true);
            Physics2D.IgnoreLayerCollision(Projectile, Player, true);
            Physics2D.IgnoreLayerCollision(Projectile, Pickup, true);
            Physics2D.IgnoreLayerCollision(Projectile, Default, true);

            Physics2D.IgnoreLayerCollision(Pickup, Pickup, true);
            Physics2D.IgnoreLayerCollision(Pickup, Enemy, true);
            Physics2D.IgnoreLayerCollision(Pickup, Player, true);
            Physics2D.IgnoreLayerCollision(Pickup, Default, true);

            // Boss shots hit the player only - they must pass through the boss's own adds.
            Physics2D.IgnoreLayerCollision(EnemyProjectile, Enemy, true);
            Physics2D.IgnoreLayerCollision(EnemyProjectile, EnemyProjectile, true);
            Physics2D.IgnoreLayerCollision(EnemyProjectile, Projectile, true);
            Physics2D.IgnoreLayerCollision(EnemyProjectile, Pickup, true);
            Physics2D.IgnoreLayerCollision(EnemyProjectile, Default, true);

            Physics2D.IgnoreLayerCollision(Projectile, Enemy, false);
            Physics2D.IgnoreLayerCollision(Enemy, Enemy, false);
            Physics2D.IgnoreLayerCollision(Enemy, Player, false);
            Physics2D.IgnoreLayerCollision(EnemyProjectile, Player, false);
        }
    }
}
