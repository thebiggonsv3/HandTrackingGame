using Godot;

public partial class Player : CharacterBody2D
{
    // Nodes
    public AnimatedSprite2D AnimatedSprite { get; private set; }

    // Shared values
    public float Gravity => ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

    // Systems
    // get means that this is read only from other files, but can be set from this file.
    public PlayerMovement Movement { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerHealth Health { get; private set; }

    // "this" indicates that Player is being used
    public override void _Ready()
    {
        GD.PrintErr("Player: _Ready() loaded");
        AnimatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        Movement = new PlayerMovement(this);
        Combat = new PlayerCombat(this);
        Health = new PlayerHealth(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        Health.PhysicsProcess(delta);

        // Don't continue if the player just respawned.
        if (GlobalPosition.Y > PlayerHealth.DeathY)
            return;

        Movement.PhysicsProcess(delta);
        Combat.PhysicsProcess(delta);
    }
}