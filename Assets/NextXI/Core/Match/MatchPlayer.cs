using System;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    /// <summary>What the body is doing right now. The animation layer maps these 1:1 to clips/poses.</summary>
    public enum PlayerAction
    {
        None,
        Pass,
        LoftedPass,
        Cross,
        Shot,
        Volley,
        Header,
        Clearance,
        Throw,
        Tackle,
        Slide,
        Jump,
        KeeperDive,
        KeeperCatch,
        KeeperThrow,
        KeeperKick,
        Fall,
        Stumble,
        Celebrate,
        Dejected,
        Trap,
        Skill
    }

    public enum KickKind { GroundPass, ThroughPass, Lofted, Cross, Shot, Clearance, Throw, KeeperThrow, KeeperKick, Header, Volley }

    /// <summary>Kick queued during the wind-up of a kicking animation, applied at the contact frame.</summary>
    [Serializable]
    public struct PendingKick
    {
        public KickKind Kind;
        public Vec3 Target;
        /// <summary>0..1 requested power (shots) or arrival-speed factor (passes).</summary>
        public float Power;
        public int ReceiverId;
        /// <summary>Extra aim error multiplier (assist level, difficulty).</summary>
        public float ErrorScale;
        public bool UserInitiated;
        /// <summary>Side spin sign for curled shots/crosses (-1, 0, +1).</summary>
        public float Curl;
        /// <summary>True when this kick is an attempt on goal (shot, volley or header at goal).</summary>
        public bool IsShot;
    }

    [Serializable]
    public sealed class MatchPlayerStats
    {
        public int Goals, Assists, Shots, ShotsOnTarget, Passes, PassesCompleted, KeyPasses;
        public int Tackles, TacklesWon, Interceptions, Saves, Fouls, FoulsSuffered, YellowCards, RedCards, Offsides;
        public int GoalsConceded, DribblesWon, Clearances, Crosses;
        public float DistanceMeters;
        public float VirtualMinutesPlayed;
        public float XG;
        public float Rating = 6f;

        public float PassAccuracy => Passes == 0 ? 0 : (float)PassesCompleted / Passes;
    }

    /// <summary>
    /// Runtime state of one footballer inside a match: kinematics, action state machine,
    /// stamina, AI intent and statistics.
    /// </summary>
    public sealed class MatchPlayer
    {
        public readonly int Id;
        public readonly PlayerProfile Profile;
        public readonly PlayerAttributes A;
        public int Team;
        public int SlotIndex;
        public TacticalRole Role;
        public Position Position;
        public int ShirtNumber;

        // Kinematics (flat, Y is the jump offset of the body)
        public Vec3 Pos;
        public Vec3 PrevPos;
        public Vec3 Vel;
        public float Facing; // radians, world flat angle
        public float JumpHeight;
        public float JumpVel;

        // Action state machine
        public PlayerAction Action;
        public float ActionTime;
        public float ActionDuration;
        public float ContactTime;
        public bool ContactDone;
        public PendingKick Kick;
        public bool KickWithWeakFoot;
        /// <summary>Direction of a dive/slide (flat unit vector).</summary>
        public Vec3 ActionDir;
        /// <summary>Initial speed of a slide or dive.</summary>
        public float ActionSpeed;
        /// <summary>Seconds during which the player cannot touch the ball (after kicking it).</summary>
        public float TouchCooldown;
        /// <summary>Seconds until the player recovers control of the body (after falls, failed tackles).</summary>
        public float Stun;
        public float NextTouchTime;
        public bool Shielding;
        public bool Jockeying;
        /// <summary>Desired dribbling direction (flat unit vector) set by AI or user input.</summary>
        public Vec3 DribbleDir;
        /// <summary>First-time action queued by the user before the ball arrives (valid while timer > 0).</summary>
        public PendingKick QueuedFirstTime;
        public float QueuedFirstTimeTimer;
        /// <summary>Set by AI when the player is going for an aerial ball and should jump/head it.</summary>
        public bool WantsHeader;
        /// <summary>User asked for the ball (seconds remaining).</summary>
        public float CallForPassTimer;
        public float CallForThroughTimer;

        // Condition
        public float Stamina = 1f;
        public bool Injured;
        public bool OnPitch = true;
        public bool SentOff;
        public int Yellow;

        // Control
        public bool UserControlled;
        public bool Starter;

        // AI scratch data
        public Vec3 Target;
        public float TargetSpeedFactor = 0.7f;
        public float DecisionTimer;
        public float RunTimer;
        public Vec3 RunTarget;
        public float HoldTimer;
        public float PressTime;
        public string Intent = "";

        public readonly MatchPlayerStats Stats = new MatchPlayerStats();

        public MatchPlayer(int id, PlayerProfile profile, int team)
        {
            Id = id;
            Profile = profile;
            A = profile.Attributes;
            Team = team;
            Position = profile.Position;
            ShirtNumber = profile.ShirtNumber;
        }

        public bool IsGoalkeeper => Role == TacticalRole.Goalkeeper;
        public bool IsActive => OnPitch && !SentOff;
        public bool IsBusy => Action != PlayerAction.None && Action != PlayerAction.Celebrate && Action != PlayerAction.Dejected;
        public bool CanAct => IsActive && Stun <= 0f && !IsBusy;
        public bool IsGrounded => JumpHeight <= 0.001f;

        public Vec3 FacingDir => Vec3.FromAngle(Facing);
        public Vec3 RightDir => Vec3.FromAngle(Facing - MathUtil.Pi * 0.5f);
        public float Speed => Vel.FlatLength;

        public float ActionProgress => ActionDuration > 0 ? MathUtil.Clamp01(ActionTime / ActionDuration) : 0f;

        /// <summary>Top sprint speed (m/s) from Pace, reduced by fatigue and injury.</summary>
        public float MaxSpeed
        {
            get
            {
                float s = MathUtil.Attr(A.Pace, 6.6f, 9.5f);
                if (IsGoalkeeper) s *= 0.9f;
                s *= 0.84f + 0.16f * Stamina;
                if (Injured) s *= 0.85f;
                return s;
            }
        }

        public float Acceleration => MathUtil.Attr(A.Acceleration, 4.2f, 7.6f) * (0.88f + 0.12f * Stamina);

        /// <summary>Velocity heading turn rate (rad/s) at a given speed.</summary>
        public float TurnRate(float speed)
        {
            float baseRate = MathUtil.Attr(A.Agility, 6f, 11f);
            return baseRate / (1f + speed * 0.22f);
        }

        /// <summary>Distance at which the player can play the ball with the feet.</summary>
        public float FootReach => 0.75f + MathUtil.Attr(A.Reactions, 0f, 0.15f);

        /// <summary>Strong-foot contact point slightly ahead of the body.</summary>
        public Vec3 FootPoint(bool weak = false)
        {
            bool rightFooted = Profile.PreferredFoot == Foot.Right;
            float side = (rightFooted ^ weak) ? 1f : -1f;
            return Pos + FacingDir * 0.42f + RightDir * (0.11f * side);
        }

        public void StartAction(PlayerAction action, float duration, float contactTime)
        {
            Action = action;
            ActionTime = 0;
            ActionDuration = duration;
            ContactTime = contactTime;
            ContactDone = false;
        }

        /// <summary>Clears the current action unless the player is on the ground.</summary>
        public void ClearActionIfInterruptible()
        {
            if (Action != PlayerAction.None && Action != PlayerAction.Fall) ClearAction();
        }

        public void ClearAction()
        {
            Action = PlayerAction.None;
            ActionTime = 0;
            ActionDuration = 0;
            ContactDone = false;
        }

        public override string ToString() => $"#{ShirtNumber} {Profile.ShortName} ({Position}, T{Team})";
    }
}
