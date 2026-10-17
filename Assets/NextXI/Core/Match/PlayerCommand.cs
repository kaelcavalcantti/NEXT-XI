using System;

namespace NextXI.Core.Match
{
    /// <summary>
    /// One frame of user intent, already converted to world space by the input layer
    /// (camera-relative stick -> world XZ). Button "Pressed" flags are edge-triggered:
    /// the input layer sets them for exactly one simulation step when a button is released
    /// (so hold-to-power can be measured), the engine consumes them.
    /// </summary>
    [Serializable]
    public struct PlayerCommand
    {
        /// <summary>World-space desired movement (flat), magnitude 0..1.</summary>
        public Vec3 Move;
        public bool Sprint;

        // Edge-triggered actions (context dependent, see UserContext)
        public bool Pass;
        public bool Through;
        public bool Lob;
        public bool Shoot;
        public bool Skill;
        public bool Switch;
        /// <summary>0..1 power measured from the hold duration of the button that was released.</summary>
        public float Power;

        // Held actions
        public bool ShieldHeld;
        public bool JockeyHeld;

        public bool AnyAction => Pass || Through || Lob || Shoot || Skill || Switch;

        public void ClearEdges()
        {
            Pass = Through = Lob = Shoot = Skill = Switch = false;
            Power = 0;
        }
    }

    /// <summary>What the four action buttons currently mean for the user (drives button labels).</summary>
    public enum UserContext
    {
        /// <summary>User has the ball: Pass / Through / Lob-Cross / Shoot.</summary>
        Attacking,
        /// <summary>Team has the ball, user doesn't: Call pass / Call through / Switch / -.</summary>
        SupportingAttack,
        /// <summary>Opponent has the ball: Tackle / Press-Contain / Switch / Slide.</summary>
        Defending,
        /// <summary>User is the taker of a set piece.</summary>
        SetPiece,
        /// <summary>No control (stoppages, celebrations, user off the pitch).</summary>
        None
    }
}
