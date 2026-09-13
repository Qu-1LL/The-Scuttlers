namespace TriloGame.Game.Core.Combat;

// A shared read-only view of health; each entity still owns damage, healing, and removal.
public interface IHealth
{
    int Health { get; }

    int MaxHealth { get; }
}
