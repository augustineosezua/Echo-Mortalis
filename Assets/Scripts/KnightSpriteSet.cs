using System;
using UnityEngine;

public enum KnightAnimationClipId
{
    None,
    Idle,
    Run,
    Jump,
    JumpFallInbetween,
    Fall,
    TurnAround,
    Attack,
    Attack2,
    AttackNoMovement,
    Attack2NoMovement,
    AttackCombo,
    AttackComboNoMovement,
    Dash,
    Roll,
    Hit,
    Death,
    DeathNoMovement,
    Crouch,
    CrouchTransition,
    CrouchWalk,
    CrouchAttack,
    CrouchAll,
    Slide,
    SlideAll,
    SlideTransitionStart,
    SlideTransitionEnd,
    WallHang,
    WallSlide,
    WallClimb,
    WallClimbNoMovement
}

[Serializable]
public struct KnightSpriteClip
{
    public Sprite[] frames;
    public float framesPerSecond;
    public bool loop;

    public bool HasFrames => frames != null && frames.Length > 0;
    public float Duration => HasFrames ? frames.Length / Mathf.Max(0.01f, framesPerSecond) : 0f;
}

[CreateAssetMenu(menuName = "Echo Mortalis/Knight Sprite Set", fileName = "PlayerKnightSet")]
public class KnightSpriteSet : ScriptableObject
{
    [Header("Core")]
    [SerializeField] private KnightSpriteClip idle;
    [SerializeField] private KnightSpriteClip run;
    [SerializeField] private KnightSpriteClip jump;
    [SerializeField] private KnightSpriteClip jumpFallInbetween;
    [SerializeField] private KnightSpriteClip fall;
    [SerializeField] private KnightSpriteClip turnAround;

    [Header("Combat")]
    [SerializeField] private KnightSpriteClip attack;
    [SerializeField] private KnightSpriteClip attack2;
    [SerializeField] private KnightSpriteClip attackNoMovement;
    [SerializeField] private KnightSpriteClip attack2NoMovement;
    [SerializeField] private KnightSpriteClip attackCombo;
    [SerializeField] private KnightSpriteClip attackComboNoMovement;
    [SerializeField] private KnightSpriteClip dash;
    [SerializeField] private KnightSpriteClip roll;
    [SerializeField] private KnightSpriteClip hit;
    [SerializeField] private KnightSpriteClip death;
    [SerializeField] private KnightSpriteClip deathNoMovement;

    [Header("Crouch And Slide")]
    [SerializeField] private KnightSpriteClip crouch;
    [SerializeField] private KnightSpriteClip crouchTransition;
    [SerializeField] private KnightSpriteClip crouchWalk;
    [SerializeField] private KnightSpriteClip crouchAttack;
    [SerializeField] private KnightSpriteClip crouchAll;
    [SerializeField] private KnightSpriteClip slide;
    [SerializeField] private KnightSpriteClip slideAll;
    [SerializeField] private KnightSpriteClip slideTransitionStart;
    [SerializeField] private KnightSpriteClip slideTransitionEnd;

    [Header("Wall")]
    [SerializeField] private KnightSpriteClip wallHang;
    [SerializeField] private KnightSpriteClip wallSlide;
    [SerializeField] private KnightSpriteClip wallClimb;
    [SerializeField] private KnightSpriteClip wallClimbNoMovement;

    public KnightSpriteClip GetClip(KnightAnimationClipId clipId)
    {
        switch (clipId)
        {
            case KnightAnimationClipId.Idle:
                return idle;
            case KnightAnimationClipId.Run:
                return run;
            case KnightAnimationClipId.Jump:
                return jump;
            case KnightAnimationClipId.JumpFallInbetween:
                return jumpFallInbetween;
            case KnightAnimationClipId.Fall:
                return fall;
            case KnightAnimationClipId.TurnAround:
                return turnAround;
            case KnightAnimationClipId.Attack:
                return attack;
            case KnightAnimationClipId.Attack2:
                return attack2;
            case KnightAnimationClipId.AttackNoMovement:
                return attackNoMovement;
            case KnightAnimationClipId.Attack2NoMovement:
                return attack2NoMovement;
            case KnightAnimationClipId.AttackCombo:
                return attackCombo;
            case KnightAnimationClipId.AttackComboNoMovement:
                return attackComboNoMovement;
            case KnightAnimationClipId.Dash:
                return dash;
            case KnightAnimationClipId.Roll:
                return roll;
            case KnightAnimationClipId.Hit:
                return hit;
            case KnightAnimationClipId.Death:
                return death;
            case KnightAnimationClipId.DeathNoMovement:
                return deathNoMovement;
            case KnightAnimationClipId.Crouch:
                return crouch;
            case KnightAnimationClipId.CrouchTransition:
                return crouchTransition;
            case KnightAnimationClipId.CrouchWalk:
                return crouchWalk;
            case KnightAnimationClipId.CrouchAttack:
                return crouchAttack;
            case KnightAnimationClipId.CrouchAll:
                return crouchAll;
            case KnightAnimationClipId.Slide:
                return slide;
            case KnightAnimationClipId.SlideAll:
                return slideAll;
            case KnightAnimationClipId.SlideTransitionStart:
                return slideTransitionStart;
            case KnightAnimationClipId.SlideTransitionEnd:
                return slideTransitionEnd;
            case KnightAnimationClipId.WallHang:
                return wallHang;
            case KnightAnimationClipId.WallSlide:
                return wallSlide;
            case KnightAnimationClipId.WallClimb:
                return wallClimb;
            case KnightAnimationClipId.WallClimbNoMovement:
                return wallClimbNoMovement;
            default:
                return default;
        }
    }
}
