using Godot;

public partial class Enemy1 : CharacterBody2D
{
    [Export]
    public int MaxHP = 100;

    [Export]
    public float Speed = 100f;

    [Export]
    public float PatrolDistance = 150f;

    private int _currentHP;

    private Vector2 _startPosition;
    private int _direction = 1;

    public override void _Ready()
    {
        _currentHP = MaxHP;
        AddToGroup("enemies");

        // Remember where the enemy started
        _startPosition = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Move horizontally
        Velocity = new Vector2(_direction * Speed, Velocity.Y);

        // Turn around at patrol limits
        if (GlobalPosition.X >= _startPosition.X + PatrolDistance)
            _direction = -1;

        if (GlobalPosition.X <= _startPosition.X - PatrolDistance)
            _direction = 1;

        MoveAndSlide();
    }

    // Damage mechanic
    public void TakeDamage(int damage)
    {
        _currentHP -= damage;

        if (_currentHP <= 0)
        {
            Die();
        }
    }

    // Goes kaboom when death
    private async void Die()
    {
        await ToSignal(GetTree().CreateTimer(1f), "timeout");
        QueueFree();
    }
}