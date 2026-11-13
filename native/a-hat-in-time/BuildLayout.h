#ifndef HAT_BUILD_LAYOUT_H
#define HAT_BUILD_LAYOUT_H

/* Steam 25577316. Offsets observed in the running 64-bit executable. */
enum {
    HatNamesRva = 0x125cce8,
    HatObjectsRva = 0x125cd90,
    HatNameText = 0x14,
    HatObjectOuter = 0x40,
    HatObjectName = 0x48,
    HatObjectClass = 0x50,
    HatClassSuper = 0x78,
    HatPropertyOffset = 0x8c,
    HatActorLocation = 0x80,
    HatActorWorld = 0x144,
    HatControllerPawn = 0x2fc,
    HatControllerGodMode = 0x318,
    HatPawnMovementSpeedModifier = 0x424,
    HatPawnJumpHeight = 0x410,
    HatPawnInvisibleToAI = 0x66c,
    HatPawnJumpFlags = 0xcd4,
    HatPawnWeapon = 0x588,
    HatWeaponDamage = 0x36c,
    HatPawnHealth = 0x458,
    HatPawnHealthMax = 0x45c,
    HatWorldTimeDilation = 0x62c,
    HatWorldNetMode = 0x6b4,
    HatGuardDetectMultiplier = 0xb00,
    HatGuardAlertness = 0xb24
};

#endif
