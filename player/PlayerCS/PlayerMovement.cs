using Godot;

public class PlayerMovement
{
    private readonly Player _player;

    private const float Speed = 400f;
    private const float DashSpeed = 700f;
    private const float DashDuration = 0.2f;
    private const float DashCooldown = 1f;
    private const float JumpVelocity = -500f;
    private const float CoyoteTime = 0.1f;
    private const float JumpBuffer = 0.1f;
    private const float GestureDirectionDuration = 0.2f;
    private const int MaxJumps = 2;

    private int _jumpCount;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private float _dashDirection = 1f;
    private float _gestureDirection;
    private float _gestureDuration;

    public float Direction { get; private set; }

    public PlayerMovement(Player player)
    {
        _player = player;
    }

    public void RequestGestureDirection(float direction)
    {
        if (_gestureDuration > 0f && direction != 0f && _gestureDirection != 0f &&
            Mathf.Sign(direction) != Mathf.Sign(_gestureDirection))
        {
            GD.PrintErr("PlayerMovement: ignoring opposite gesture direction while current gesture is active");
            return;
        }

        GD.PrintErr($"PlayerMovement: RequestGestureDirection {direction}");
        _gestureDirection = direction;
        _gestureDuration = GestureDirectionDuration;
    }

    public void RequestGestureJump()
    {
        GD.PrintErr("PlayerMovement: RequestGestureJump");
        _jumpBufferTimer = JumpBuffer;
    }

    public void PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 velocity = _player.Velocity;

        _dashTimer -= dt;
        _dashCooldownTimer -= dt;

        if (_player.IsOnFloor())
        {
            _coyoteTimer = CoyoteTime;
            _jumpCount = 0;
        }
        else
        {
            velocity.Y += _player.Gravity * dt;
            _coyoteTimer -= dt;
        }

        _jumpBufferTimer -= dt;
        _gestureDuration -= dt;

        // CHANGE: Handle all jump input BEFORE consuming the jump buffer.

        if (Input.IsActionJustPressed("jump"))
            _jumpBufferTimer = JumpBuffer;

        if (_gestureDuration > 0)
        {
            Direction = _gestureDirection;
        }
        else
        {
            Direction = Input.GetAxis("move_left", "move_right");
        }

        if (_jumpBufferTimer > 0 &&
            (_coyoteTimer > 0 || _jumpCount < MaxJumps))
        {
            velocity.Y = JumpVelocity;
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _jumpCount++;
        }

        if (_gestureDuration > 0)
            GD.PrintErr($"PlayerMovement: using gesture direction={Direction} duration={_gestureDuration}");

        if (Input.IsActionJustReleased("jump") && velocity.Y < 0)
            velocity.Y *= 0.5f;

        if (_player.AnimatedSprite.Animation != "attack" ||
            !_player.AnimatedSprite.IsPlaying())
        {
            if (Direction != 0)
            {
                _player.AnimatedSprite.Play("run");
                _player.AnimatedSprite.FlipH = Direction < 0;
            }
            else
            {
                _player.AnimatedSprite.Stop();
            }
        }

        if (Direction != 0)
            _dashDirection = Direction;

        if (Input.IsActionJustPressed("sprint") &&
            _dashCooldownTimer <= 0 &&
            _dashTimer <= 0)
        {
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
        }

        if (_dashTimer > 0)
            velocity.X = _dashDirection * DashSpeed;
        else if (Direction != 0)
            velocity.X = Direction * Speed;
        else
            velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);

        _player.Velocity = velocity;
        _player.MoveAndSlide();
    }
}