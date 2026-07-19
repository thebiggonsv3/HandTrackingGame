using Godot;

public class PlayerHealth
{
    private readonly Player _player;

    public const int MaxHealth = 100;
    public const float DeathY = 2000f;

    private int _health;
    private Vector2 _spawnPosition;

    public int Health => _health;

    public PlayerHealth(Player player)
    {
        _player = player;
        _health = MaxHealth;
        _spawnPosition = player.GlobalPosition;
    }

    public void PhysicsProcess(double delta)
    {
        if (_player.GlobalPosition.Y > DeathY)
            Respawn();
    }

    public void TakeDamage(int damage)
    {
        _health -= damage;

        if (_health <= 0)
            Respawn();
    }

    private void Respawn()
    {
        _player.GlobalPosition = _spawnPosition;
        _player.Velocity = Vector2.Zero;
        _health = MaxHealth;
    }
}