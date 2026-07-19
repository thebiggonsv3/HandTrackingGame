using Godot;

public class PlayerCombat
{
    private readonly Player _player;

    private const float AttackRange = 150f;
    private const int AttackDamage = 25;
    private const float AttackCooldown = 0.5f;

    private float _attackCooldown;

    public PlayerCombat(Player player)
    {
        _player = player;
    }

    public void PhysicsProcess(double delta)
    {
        _attackCooldown -= (float)delta;

        if (Input.IsActionJustPressed("attack") &&
            _attackCooldown <= 0)
        {
            _player.AnimatedSprite.Play("attack");

            foreach (Node node in _player.GetTree().GetNodesInGroup("enemies"))
            {
                if (node is Node2D enemy &&
                    _player.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= AttackRange)
                {
                    enemy.Call("TakeDamage", AttackDamage);
                }
            }

            _attackCooldown = AttackCooldown;
        }
    }
}