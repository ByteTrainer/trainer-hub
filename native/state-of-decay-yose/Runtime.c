#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdbool.h>
#include <math.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <wchar.h>
#include "MinHook.h"

typedef void *(__thiscall *GetVariable)(void *, const char *);
typedef int (__thiscall *GetInt)(void *);
typedef float (__thiscall *GetFloat)(void *);
typedef void (__thiscall *SetInt)(void *, int);
typedef void (__thiscall *SetFloat)(void *, float);
typedef void *(__thiscall *GetTimeOfDay)(void *);
typedef void (__thiscall *SetHour)(void *, float, bool);
typedef BOOL (WINAPI *PeekMessageFn)(LPMSG, HWND, UINT, UINT, UINT);

enum {
    Health, Stamina, Loot, Radio, Items, Durability, Morning, Suppressor, Weight,
    NoReload, Ammo, Speed, Resources, OneHit, Recoil, Stealth, ToughNpc, GodMode,
    VehicleRepair, Build, Experience, SurvivorImmunity, JumpHeight, Count
};
static struct {
    const char *name;
    bool floating;
    float enabled;
    float saved;
} variables[Count] = {
    [Health] = {"pl_infinite_health", false, 1, 0},
    [Stamina] = {"pl_infinite_stamina", false, 1, 0},
    [Loot] = {NULL, true, 0.15f, 0},
    [Radio] = {"ro_test", true, 1, 0},
    [Items] = {"ch_infiniteConsumables", false, 1, 0},
    [Durability] = {"ch_infiniteDurability", false, 1, 0},
    [Morning] = {"e_TimeOfDaySpeed", true, 0, 0},
    [Stealth] = {"zm_playerInvisible", false, 1, 0},
};

static const struct {
    unsigned command;
    size_t offset;
    size_t size;
    uint8_t original[14];
    uint8_t enabled[14];
} patches[] = {
    // Skip suppressor wear and continue into the other per-shot updates.
    {Suppressor, 0x109a6bc, 5, {0x76, 0x17, 0x8b, 0x4f, 0x04}, {0xe9, 0x9c, 0, 0, 0}},
    // Clear the local survivor's total, including the separately equipped rucksack.
    {Weight, 0x10a27c0, 9, {0xf3, 0x0f, 0x10, 0x44, 0x24, 0x08, 0x0f, 0x2f, 0xc1},
        {0x83, 0x64, 0x24, 0x08, 0x00, 0x0f, 0x2f, 0xc1, 0x90}},
    // Take the unencumbered branch even when a condition reduces the limit to zero.
    {Weight, 0x10a27d4, 2, {0x72, 0x1f}, {0xeb, 0x1f}},
};

static const unsigned All = (1u << Count) - 1;
static uint8_t *image;
static PeekMessageFn originalPeek;
static unsigned applied;
static DWORD pid;
static float movementMultiplier = 1.5f;
static float experienceMultiplier = 2.0f;
static float jumpHeightMultiplier = 1.0f;
static char session[33];
static wchar_t settings[MAX_PATH], status[MAX_PATH], pending[MAX_PATH];

typedef void *(__thiscall *GetGameObject)(void *);
typedef float (__thiscall *ReadInventoryWeight)(const void *);
typedef void (__thiscall *UpdateEncumbrance)(void *);
typedef struct {
    float rotation[4];
    float translation[3];
} MovementTransform;
typedef void (__thiscall *ApplyPhysicalMovement)(void *, const MovementTransform *,
                                                 const MovementTransform *);
typedef struct {
    uint32_t source, target, weapon, flags;
    float damage;
    uint8_t metadata[0x64];
} CombatHit;
_Static_assert(sizeof(CombatHit) == 0x78, "CGameRules hit layout");
typedef void (__thiscall *ProcessCombatHit)(void *, const CombatHit *);
typedef void (__thiscall *KillActor)(void *, void *, uint32_t, uint32_t, const CombatHit *);
typedef void *(__thiscall *FindActor)(void *, uint32_t);
typedef int (__thiscall *GetActorGodMode)(void *);
typedef bool (__cdecl *TickActorState)(uint8_t *, bool, void *);
typedef bool (__thiscall *StartGrab)(void *);
typedef void *(__thiscall *LinkActorToEntity)(void *, uint32_t, uint32_t);
typedef struct { float x, y, z; } CombatVector;
typedef struct {
    const void *vtable;
    uint32_t source, target;
    uint8_t parameters[0x34];
    const void *sourceActor;
} MeleeContact;
_Static_assert(offsetof(MeleeContact, sourceActor) == 0x40, "native combat contact layout");
typedef void (__thiscall *ApplyMeleeContact)(void *, const MeleeContact *);
typedef CombatVector *(__cdecl *SolveMovementLaunch)(CombatVector *, const uint8_t *, float, float,
                                                     CombatVector, CombatVector);
typedef void (__thiscall *ZombieMeleeSweep)(void *, CombatVector, CombatVector, float, void *, float, float);
typedef bool (__thiscall *QueueZombieReaction)(void *, unsigned, float, uint32_t);
typedef float (__thiscall *WeaponRecoil)(void *);
typedef void *(__thiscall *WeaponOwner)(void *);
typedef void (__thiscall *FinishReload)(void *, uint32_t);
typedef void (__cdecl *ChangeStock)(uint32_t, float);
typedef float (__cdecl *ReadStock)(uint32_t);
typedef uint32_t *(__cdecl *FindCharacterHandle)(uint32_t *, uint32_t);
typedef void *(__cdecl *FindCharacter)(uint32_t);
typedef bool (__thiscall *HasCharacterTrait)(const void *, uint32_t);
typedef void (__thiscall *AddCharacterTrait)(void *, const uint32_t *);
typedef void (__thiscall *RemoveCharacterTraitSlot)(void *, unsigned);
typedef void (__thiscall *SetCharacterValue)(void *, float);
typedef void (__thiscall *SetHealth)(void *, float);
typedef void (__thiscall *FallAndPlay)(void *, uint32_t, uint32_t, uint32_t, uint32_t);
typedef void (__thiscall *CharacterDown)(void *);
typedef void (__thiscall *ActorDeath)(void *, uint32_t, uint32_t, uint32_t, uint32_t);
typedef void (__cdecl *NotifyCharacterDeath)(uint32_t);
typedef void (__thiscall *AwardExpertise)(void *, uint32_t, int);
typedef void (__thiscall *FacilityUpdate)(void *, float);
typedef uint32_t *(__thiscall *MakeObjectHandle)(uint32_t *, unsigned, unsigned, unsigned);
typedef float (__cdecl *ReadActionTimer)(uint32_t, unsigned);
typedef void (__cdecl *AdjustActionTimer)(uint32_t, unsigned, float);
typedef void *(__thiscall *FindVehicleRecord)(void *, uint32_t);
typedef void (__thiscall *RepairVehicle)(void *, uint32_t);
typedef void (__thiscall *HitVehicleComponent)(void *, uint32_t, uint32_t, float,
    uint32_t, uint32_t, uint32_t, uint32_t, uint32_t, uint32_t, uint32_t);
typedef void (__thiscall *RegisterStimulus)(void *, const uint32_t *);
static ApplyPhysicalMovement originalPhysicalMovement;
static ProcessCombatHit originalCombatHit;
static StartGrab originalStartGrab;
static ProcessCombatHit originalClientHit;
static KillActor originalKillActor;
static GetActorGodMode originalActorGodMode;
static TickActorState originalActorState;
static LinkActorToEntity originalLinkActor;
static ZombieMeleeSweep originalMeleeSweep;
static ApplyMeleeContact originalMeleeContact;
static SolveMovementLaunch originalMovementLaunch;
static QueueZombieReaction originalZombieReaction;
static DWORD meleeSweepThreadState = TLS_OUT_OF_INDEXES;
static _Alignas(8) volatile LONG64 pendingDeaths[256];
static ReadInventoryWeight originalInventoryWeight;
static ChangeStock originalChangeStock;
static WeaponRecoil originalRecoil, originalRecoilLimit;
static FinishReload originalFinishReload;
static bool hooksReady;
static bool protectionReady, allyProtectionReady;
static bool combatHitReady, clientHitReady, killReady, nativeGodReady;
static bool actorStateReady, grabReady, entityLinkReady, meleeContactReady;
static SetHealth originalSetHealth;
static SetHealth originalBaseSetHealth, originalPlayerSetHealth;
static FallAndPlay originalFallAndPlay;
static CharacterDown originalCharacterDown;
static ActorDeath originalActorDeath;
static NotifyCharacterDeath originalNotifyCharacterDeath;
static AddCharacterTrait originalAddTrait;
static SetCharacterValue originalSetFatigue, originalSetWounds;
static bool survivorImmunityReady;
static AwardExpertise originalAwardExpertise;
static FacilityUpdate originalFacilityUpdate;
static HitVehicleComponent originalHitVehicleComponent;
static RegisterStimulus originalRegisterStimulus;
static volatile LONG stealthActorId, stealthVehicleId;
static bool buildReady, experienceReady, movementReady, weightReady, jumpReady;

static void *gameFramework(void) {
    void *framework = *(void **)(image + 0x1a1e638);
    return framework ? ((GetGameObject)(*(void ***)framework)[0x38 / 4])(framework) : NULL;
}

static void *controlledActor(void) {
    void *game = gameFramework();
    return game ? ((GetGameObject)(*(void ***)game)[0x114 / 4])(game) : NULL;
}

static uint8_t *occupiedVehicle(void *actor) {
    return actor ? ((GetGameObject)(*(void ***)actor)[0x184 / 4])(actor) : NULL;
}

static uint32_t entityId(const void *object) {
    return object ? *(const uint32_t *)((const uint8_t *)object + 8) : 0;
}

static unsigned liveZombieIndex(const uint8_t *manager, uint32_t id) {
    if (!manager || !id) return 256;
    const uint8_t *record = manager + 0x4ae0;
    for (unsigned i = 0; i < 24; ++i, record += 0x10) {
        if (record[0x0d] && *(const uint32_t *)(record + 4) == id)
            return *(const uint32_t *)record;
    }
    return 256;
}

static bool localGodActor(const void *actor) {
    return actor && (applied & (1u << GodMode)) && actor == controlledActor();
}

static bool ordinaryZombie(unsigned index) {
    if (index >= 256) return false;
    const uint16_t *variants = (const uint16_t *)(image + 0x19f5180);
    unsigned species = variants[index] & 0xff;
    return species == 0 || species == 6 || species == 8; // Grunt and the two FreshGrunt entries.
}

static void scheduleZombieDeath(unsigned index, uint32_t id, uint32_t source) {
    InterlockedExchange64(&pendingDeaths[index], (LONG64)(((uint64_t)source << 32) | id));
}

static void finishPendingZombieDeaths(void) {
    bool enabled = (applied & (1u << OneHit)) != 0;
    void *rules = NULL;
    uint32_t local = 0;
    FindActor find = (FindActor)(image + 0x1128130);
    for (unsigned index = 0; index < 256; ++index) {
        uint64_t death = (uint64_t)InterlockedExchange64(&pendingDeaths[index], 0);
        uint32_t id = (uint32_t)death;
        if (!enabled || !id) continue;
        const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
        if (liveZombieIndex(manager, id) != index || !ordinaryZombie(index)) continue;
        if (!rules) {
            void *framework = gameFramework();
            if (!framework) return;
            local = entityId(((GetGameObject)(*(void ***)framework)[0x114 / 4])(framework));
            if (!local) return;
            if ((uint32_t)(death >> 32) != local) continue;
            // Match the engine's KillPlayer binding: framework -> rules system -> current rules.
            void *system = ((GetGameObject)(*(void ***)framework)[0x80 / 4])(framework);
            if (!system) return;
            rules = ((GetGameObject)(*(void ***)system)[0x28 / 4])(system);
            if (!rules || (*(void ***)rules)[0x170 / 4] != image + 0x112a470) return;
        }
        if ((uint32_t)(death >> 32) != local) continue;
        void *actor = find(rules, id);
        if (!actor || *(const float *)((const uint8_t *)actor + 0x58) <= 0.0f) continue;
        const CombatHit hit = {.source = local, .target = id, .damage = 10000.0f};
        originalKillActor(rules, actor, 0, 1, &hit);
    }
}

static bool protectedActor(void *actor);
static bool protectedAllyEntity(uint32_t id);

static bool protectedEntity(uint32_t id) {
    if (!id) return false;
    if ((applied & (1u << GodMode)) && id == entityId(controlledActor())) return true;
    return protectedAllyEntity(id);
}

static bool finishLethalHit(ProcessCombatHit process, void *rules, const CombatHit *hit,
                            uint32_t local) {
    if (!local || hit->source != local || !(hit->damage > 0.0f)) return false;
    const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
    unsigned index = liveZombieIndex(manager, hit->target);
    if (!ordinaryZombie(index)) return false;
    CombatHit lethal = *hit;
    if (lethal.damage < 10000.0f) lethal.damage = 10000.0f;
    process(rules, &lethal);
    scheduleZombieDeath(index, lethal.target, local);
    return true;
}

static void __thiscall processClientHit(void *rules, const CombatHit *hit) {
    // The melee fire mode submits a confirmed strike here before server hit filtering.
    bool melee = (applied & (1u << OneHit)) &&
                 __builtin_return_address(0) == image + 0x1257f4f;
    if (protectedEntity(hit->target)) return;
    uint32_t local = melee ? entityId(controlledActor()) : 0;
    if (!melee || !finishLethalHit(originalClientHit, rules, hit, local)) originalClientHit(rules, hit);
}

static int __thiscall actorGodMode(void *actor) {
    return localGodActor(actor) ? 2 : originalActorGodMode(actor);
}

static void __thiscall killActor(void *rules, void *actor, uint32_t drop, uint32_t ragdoll,
                                const CombatHit *hit) {
    if (!protectedActor(actor)) originalKillActor(rules, actor, drop, ragdoll, hit);
}

static void __thiscall processCombatHit(void *rules, const CombatHit *hit) {
    if (protectedEntity(hit->target)) return;
    uint32_t local = (applied & (1u << OneHit)) ? entityId(controlledActor()) : 0;
    if (!(applied & (1u << OneHit)) || !finishLethalHit(originalCombatHit, rules, hit, local))
        originalCombatHit(rules, hit);
}

static void __thiscall applyMeleeContact(void *victim, const MeleeContact *contact) {
    if (protectedActor(victim)) return;
    // CombatDamage drains its confirmed contacts here, with the attacker already resolved.
    if ((applied & (1u << OneHit)) && __builtin_return_address(0) == image + 0xf8e400 &&
        contact->sourceActor && contact->sourceActor == controlledActor() &&
        contact->source == entityId(contact->sourceActor) && contact->target == entityId(victim) &&
        *(const void *const *)((const uint8_t *)victim + 0x1100)) {
        const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
        unsigned index = liveZombieIndex(manager, contact->target);
        if (ordinaryZombie(index)) scheduleZombieDeath(index, contact->target, contact->source);
    }
    originalMeleeContact(victim, contact);
}

static void __thiscall zombieMeleeSweep(void *manager, CombatVector origin, CombatVector direction,
                                        float range, void *attacker, float noise, float reach) {
    void *previous = TlsGetValue(meleeSweepThreadState);
    uint32_t local = (applied & (1u << OneHit)) ? entityId(controlledActor()) : 0;
    TlsSetValue(meleeSweepThreadState, local && entityId(attacker) == local ? manager : NULL);
    originalMeleeSweep(manager, origin, direction, range, attacker, noise, reach);
    TlsSetValue(meleeSweepThreadState, previous);
}

static bool __thiscall queueZombieReaction(void *manager, unsigned index, float duration,
                                            uint32_t flags) {
    bool accepted = originalZombieReaction(manager, index, duration, flags);
    if (flags == 2 && TlsGetValue(meleeSweepThreadState) == manager &&
        ordinaryZombie(index) &&
        __builtin_return_address(0) == image + 0xf1a580) {
        const uint8_t *record = (const uint8_t *)manager + 0x4ae0;
        for (unsigned i = 0; i < 24; ++i, record += 0x10) {
            if (!record[0x0d] || *(const uint32_t *)record != index) continue;
            scheduleZombieDeath(index, *(const uint32_t *)(record + 4), entityId(controlledActor()));
            break;
        }
    }
    return accepted;
}

static bool __cdecl tickActorState(uint8_t *machine, bool repeat, void *output) {
    if ((applied & ((1u << GodMode) | (1u << ToughNpc))) && machine[0x18]) {
        const uint8_t *defs = *(const uint8_t **)(image + 0x1a8f49c);
        const uint8_t *definition = *(const uint8_t *const *)(machine + 4);
        const uint8_t *context = *(const uint8_t *const *)machine;
        uint16_t *next = (uint16_t *)(machine + 0x0e);
        uint16_t miss = 0;
        if (defs && definition == defs + 4 * 0x68 && (*next == 4 || *next == 13 || *next == 19))
            miss = 18; // Feral leap miss.
        else if (defs && definition == defs + 5 * 0x68 && (*next >= 17 && *next <= 19))
            miss = 21; // Juggernaut grab miss.
        else if (defs && definition == defs + 2 * 0x68 &&
                 (*next == 38 || *next == 52 || (*next >= 71 && *next <= 74)))
            miss = 1; // Ordinary zombie idle.
        if (miss && context) {
            const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
            if (liveZombieIndex(manager, entityId(*(void **)(machine + 8))) < 256 &&
                (protectedEntity(*(const uint32_t *)(context + 0x4c)) ||
                 protectedEntity(*(const uint32_t *)(context + 0x50)))) *next = miss;
        }
    }
    return originalActorState(machine, repeat, output);
}

static bool __thiscall startGrab(void *handler) {
    if (protectedEntity(*(const uint32_t *)((const uint8_t *)handler + 0x3e0))) return false;
    return originalStartGrab(handler);
}

static void *__thiscall linkActor(void *actor, uint32_t parent, uint32_t flags) {
    if (parent && (applied & ((1u << GodMode) | (1u << ToughNpc)))) {
        uint32_t child = entityId(actor);
        const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
        if ((liveZombieIndex(manager, parent) < 256 && protectedActor(actor)) ||
            (liveZombieIndex(manager, child) < 256 && protectedEntity(parent))) return NULL;
    }
    return originalLinkActor(actor, parent, flags);
}

static void __thiscall hitVehicleComponent(void *component, uint32_t target, uint32_t source,
                                          float damage, uint32_t x, uint32_t y, uint32_t z,
                                          uint32_t radius, uint32_t type, uint32_t flags,
                                          uint32_t extra) {
    if (damage > 0.0f && (applied & (1u << VehicleRepair))) {
        void *owner = *(void **)((uint8_t *)component + 4);
        uint32_t occupied = entityId(occupiedVehicle(controlledActor()));
        if (occupied && entityId(owner) == occupied) return;
    }
    originalHitVehicleComponent(component, target, source, damage, x, y, z, radius, type, flags, extra);
}

static void __thiscall registerStimulus(void *manager, const uint32_t *stimulus) {
    uint32_t source = *stimulus;
    if (source && (source == (uint32_t)InterlockedCompareExchange(&stealthActorId, 0, 0) ||
                   source == (uint32_t)InterlockedCompareExchange(&stealthVehicleId, 0, 0))) return;
    originalRegisterStimulus(manager, stimulus);
}

static void updateStealthSources(void) {
    void *actor = (applied & (1u << Stealth)) ? controlledActor() : NULL;
    InterlockedExchange(&stealthActorId, entityId(actor));
    InterlockedExchange(&stealthVehicleId, entityId(occupiedVehicle(actor)));
}

static void __thiscall awardExpertise(void *character, uint32_t skill, int points) {
    if ((applied & (1u << Experience)) && points > 0 &&
        character == *(void **)(image + 0x1a658c8)) {
        double scaled = points * (double)experienceMultiplier;
        points = scaled >= 65535.0 ? 65535 : (int)(scaled + 0.5);
    }
    originalAwardExpertise(character, skill, points);
}

static float __thiscall inventoryWeight(const void *inventory) {
    if (applied & (1u << Weight)) {
        const uint8_t *character = *(const uint8_t **)(image + 0x1a658c8);
        if (character && inventory == *(void *const *)(character + 0xe8)) return 0.0f;
    }
    return originalInventoryWeight(inventory);
}

static void __thiscall updateFacility(void *object, float elapsed) {
    const uint8_t *facility = object;
    unsigned type = *(const unsigned *)(facility + 0x0c);
    const uint8_t *types = *(const uint8_t **)(image + 0x1a8f1d8);
    if ((applied & (1u << Build)) && elapsed > 0.0f && types && type != UINT32_MAX) {
        // Match the facility's actions using the same metadata and handle as the stock game.
        const uint8_t *variants = *(const uint8_t *const *)(types + type * 32 + 0x1c);
        const uint8_t *variant = variants + *(const unsigned *)(facility + 0x10) * 40;
        unsigned first = *(const uint16_t *)(variant + 0x24);
        unsigned count = variant[0x26];
        const uint8_t *actions = *(const uint8_t *const *)(variant + 0x18);
        uint32_t handle = 0;
        ((MakeObjectHandle)(image + 0xf18d20))(&handle, 3, facility[0x6a],
                                             *(const uint16_t *)(facility + 0x68));
        ReadActionTimer readTimer = (ReadActionTimer)(image + 0x106cdf0);
        AdjustActionTimer adjustTimer = (AdjustActionTimer)(image + 0x106bf50);
        for (unsigned i = 0; i < count; ++i) {
            unsigned flags = *(const unsigned *)(actions + i * 36 + 0x1c);
            // Manual orders may have no category flag. Skip passive and background actions.
            if (flags & (0x02 | 0x80)) continue;
            float remaining = readTimer(handle, first + i);
            if (remaining > 0.05f && remaining < 1.0e6f)
                adjustTimer(handle, first + i, 0.05f - remaining);
        }
    }
    originalFacilityUpdate(object, elapsed);
}

static void repairOccupiedVehicle(void) {
    static uint32_t repairedEntity;
    void *actor = (applied & (1u << VehicleRepair)) ? controlledActor() : NULL;
    uint8_t *vehicle = occupiedVehicle(actor);
    void *manager = *(void **)(image + 0x1a56ad4);
    if (!vehicle || !manager) {
        repairedEntity = 0;
        return;
    }
    uint32_t occupied = entityId(vehicle);
    if (!occupied) return;
    void **methods = *(void ***)manager;
    uint32_t handle = 0x67ff8000;
    ((void (__thiscall *)(void *, uint32_t *, uint32_t))methods[0x90 / 4])(manager, &handle, occupied);
    if ((handle >> 27) != 12) return;
    uint8_t *record = ((FindVehicleRecord)(image + 0x100e5f0))(manager, handle);
    if (!record) return;
    unsigned count = *(unsigned *)(record + 0x20);
    if (!count || count > 64) return;
    if (occupied != repairedEntity) {
        ((RepairVehicle)(image + 0x1012c70))(manager, handle);
        repairedEntity = occupied;
        return;
    }
    for (unsigned i = 0; i < count; ++i) {
        if (*(float *)(record + 0x24 + 4 * i) > 0.0f) {
            ((RepairVehicle)(image + 0x1012c70))(manager, handle);
            break;
        }
    }
}

static bool installFeatureHook(size_t offset, const uint8_t *prologue, size_t length,
                               void *replacement, void **original) {
    if (memcmp(image + offset, prologue, length)) return false;
    MH_STATUS status = MH_Initialize();
    if (status != MH_OK && status != MH_ERROR_ALREADY_INITIALIZED) return false;
    if (MH_CreateHook(image + offset, replacement, original) != MH_OK) return false;
    if (MH_EnableHook(image + offset) == MH_OK) return true;
    MH_RemoveHook(image + offset);
    return false;
}

static bool protectedAlly(const void *object) {
    const uint8_t *character = object;
    if (!(applied & (1u << ToughNpc)) || !character ||
        character == *(const uint8_t **)(image + 0x1a658c8)) return false;
    unsigned flags = *(const unsigned *)(character + 0x160);
    if ((flags & 5) == 5) return true; // Allocated member of the player's community.
    uint32_t handle = *(const uint32_t *)(character + 0x48);
    if (handle && handle == *(const uint32_t *)(image + 0x1a658e8)) return true;
    if (!*(void **)(image + 0x1a658b0)) return false;
    HasCharacterTrait hasTrait = (HasCharacterTrait)(image + 0x10957d0);
    return hasTrait(character, 0x06080001) || // Trait.Behavior.FollowPlayer
           hasTrait(character, 0x06080004) || // Trait.Behavior.Follower
           hasTrait(character, 0x06060010);   // Trait.Meta.VIP
}

static bool immuneSurvivor(const uint8_t *character) {
    if (!(applied & (1u << SurvivorImmunity))) return false;
    unsigned flags = *(const unsigned *)(character + 0x160);
    // An allocated character must belong to the community or be the active player.
    return (flags & 1) && (flags & 0x0c);
}

static bool harmfulHealthTrait(uint32_t trait) {
    switch (trait) {
        case 0x06010002: // Hungry
        case 0x06010003: // Tired
        case 0x0601000d: // Injury
        case 0x0601000e: // Sick
        case 0x06010012: // Wound
        case 0x06010013: // LowEnergy
        case 0x060a0002: // Winded
        case 0x060a0003: // Tired fatigue level
        case 0x060a0004: // VeryTired
        case 0x060a0005: // Exhausted
            return true;
        default:
            return false;
    }
}

static void __thiscall addSurvivorTrait(void *character, const uint32_t *trait) {
    if (!immuneSurvivor(character) || !harmfulHealthTrait(*trait))
        originalAddTrait(character, trait);
}

static float restedFatigue(void) {
    const float *levels = *(const float **)(image + 0x1a6588c);
    return levels && *(const unsigned *)(image + 0x1a65890) ? levels[1] : 0.0f;
}

static void __thiscall setSurvivorFatigue(void *character, float value) {
    originalSetFatigue(character, immuneSurvivor(character) ? restedFatigue() : value);
}

static void __thiscall setSurvivorWounds(void *character, float value) {
    originalSetWounds(character, immuneSurvivor(character) ? 0.0f : value);
}

static void cureSurvivors(void) {
    if (!(applied & (1u << SurvivorImmunity))) return;
    uint8_t *characters = *(uint8_t **)(image + 0x1a4bbcc);
    const uint8_t *traits = *(const uint8_t **)(image + 0x1a658b0);
    unsigned traitCount = *(const unsigned *)(image + 0x1a658ac);
    if (!characters || !traits || !traitCount) return;

    HasCharacterTrait hasTrait = (HasCharacterTrait)(image + 0x10957d0);
    RemoveCharacterTraitSlot removeTrait = (RemoveCharacterTraitSlot)(image + 0x109c2f0);
    SetCharacterValue syncVitals = (SetCharacterValue)(image + 0x10a2b50);
    float rested = restedFatigue();
    for (unsigned i = 0; i < 100; ++i) {
        uint8_t *character = characters + i * 0x17c;
        if (!immuneSurvivor(character) || hasTrait(character, 0x0601000b)) continue; // Dead
        bool changed = false;
        const uint16_t *slots = (const uint16_t *)character;
        for (unsigned slot = 0; slot < 36; ++slot) {
            unsigned index = slots[slot];
            if (index < traitCount && harmfulHealthTrait(*(const uint32_t *)(traits + index * 0x34))) {
                removeTrait(character, slot);
                changed = true;
            }
        }
        if (*(float *)(character + 0xac) != 0.0f) {
            originalSetWounds(character, 0.0f);
            changed = true;
        }
        if (*(float *)(character + 0xb8) != rested) {
            originalSetFatigue(character, rested);
            changed = true;
        }
        float *staminaPenalty = (float *)(character + 0xb4);
        if (*staminaPenalty != 0.0f) {
            *staminaPenalty = 0.0f;
            changed = true;
        }
        if (changed) syncVitals(character, 0.0f);
    }
}

static bool protectedAllyEntity(uint32_t id) {
    if (!(applied & (1u << ToughNpc)) || !id || !*(void **)(image + 0x1a4bbcc)) return false;
    if (id == entityId(controlledActor())) return false;
    const uint8_t *manager = *(const uint8_t **)(image + 0x19f2e8c);
    if (liveZombieIndex(manager, id) < 256) return false;
    uint32_t handle = 0;
    ((FindCharacterHandle)(image + 0xfe3ea0))(&handle, id);
    return protectedAlly(((FindCharacter)(image + 0xfedb60))(handle));
}

static bool protectedActor(void *actor) {
    return localGodActor(actor) || protectedAllyEntity(entityId(actor));
}

static void __thiscall setHealth(void *actor, float health) {
    if (health < *(float *)((uint8_t *)actor + 0x58) && protectedActor(actor)) return;
    originalSetHealth(actor, health);
}

static void __thiscall setBaseHealth(void *actor, float health) {
    if (health < *(float *)((uint8_t *)actor + 0x58) && protectedActor(actor)) return;
    originalBaseSetHealth(actor, health);
}

static void __thiscall setPlayerHealth(void *actor, float health) {
    if (health < *(float *)((uint8_t *)actor + 0x58) && protectedActor(actor)) return;
    originalPlayerSetHealth(actor, health);
}

static void __thiscall fallAndPlay(void *actor, uint32_t a, uint32_t b, uint32_t c, uint32_t d) {
    if (!protectedActor(actor)) originalFallAndPlay(actor, a, b, c, d);
}

static void __thiscall characterDown(void *character) {
    if (!protectedAlly(character)) originalCharacterDown(character);
}

static bool installProtectionHooks(void) {
    static const uint8_t healthPrologue[] = {0xa1, 0x58, 0xd5, 0xee, 0x01, 0x56};
    static const uint8_t fallPrologue[] = {0x81, 0xec, 0x68, 0x02, 0x00, 0x00};
    if (memcmp(image + 0xf8c930, healthPrologue, sizeof(healthPrologue)) ||
        memcmp(image + 0xf7b810, fallPrologue, sizeof(fallPrologue))) return false;
    if (MH_Initialize() != MH_OK && !hooksReady) return false;
    if (MH_CreateHook(image + 0xf8c930, setHealth, (void **)&originalSetHealth) == MH_OK &&
        MH_CreateHook(image + 0xf7b810, fallAndPlay, (void **)&originalFallAndPlay) == MH_OK &&
        MH_QueueEnableHook(image + 0xf8c930) == MH_OK &&
        MH_QueueEnableHook(image + 0xf7b810) == MH_OK &&
        MH_ApplyQueued() == MH_OK) return true;
    MH_RemoveHook(image + 0xf8c930);
    MH_RemoveHook(image + 0xf7b810);
    return false;
}

static void __thiscall actorDeath(void *actor, uint32_t source, uint32_t weapon,
                                  uint32_t reason, uint32_t flags) {
    if (!protectedAllyEntity(entityId(actor))) originalActorDeath(actor, source, weapon, reason, flags);
}

static void __cdecl notifyCharacterDeath(uint32_t id) {
    if (!protectedAllyEntity(id)) originalNotifyCharacterDeath(id);
}

static bool installAllyProtectionHooks(void) {
    static const uint8_t basePrologue[] = {0x51, 0x83, 0x3d, 0xdc, 0x82, 0xe8, 0x01, 0};
    static const uint8_t playerPrologue[] = {0x81, 0xec, 0xdc, 0, 0, 0};
    static const uint8_t downPrologue[] = {0x83, 0xec, 0x10, 0x56, 0x8b, 0xf1};
    static const uint8_t actorDeathPrologue[] = {0x83, 0xec, 0x18, 0x53, 0x55, 0x56, 0x57};
    static const uint8_t notifyDeathPrologue[] = {0x51, 0x53, 0x8b, 0x5c, 0x24, 0x0c};
    return installFeatureHook(0xf81ab0, basePrologue, sizeof(basePrologue),
                              setBaseHealth, (void **)&originalBaseSetHealth) &&
           installFeatureHook(0x1109140, playerPrologue, sizeof(playerPrologue),
                              setPlayerHealth, (void **)&originalPlayerSetHealth) &&
           installFeatureHook(0x1095e40, downPrologue, sizeof(downPrologue),
                              characterDown, (void **)&originalCharacterDown) &&
           installFeatureHook(0xf7e320, actorDeathPrologue, sizeof(actorDeathPrologue),
                              actorDeath, (void **)&originalActorDeath) &&
           installFeatureHook(0xfe3bf0, notifyDeathPrologue, sizeof(notifyDeathPrologue),
                              notifyCharacterDeath, (void **)&originalNotifyCharacterDeath);
}

static bool installSurvivorImmunityHooks(void) {
    static const uint8_t traitPrologue[] = {0x53, 0x55, 0x8b, 0x6c, 0x24, 0x0c};
    static const uint8_t fatiguePrologue[] = {0x51, 0x83, 0x3d, 0x90, 0x58, 0xe6, 0x01, 0};
    static const uint8_t woundPrologue[] = {0x51, 0x56, 0x8b, 0xf1, 0xe8};
    return installFeatureHook(0x108d8a0, traitPrologue, sizeof(traitPrologue),
                              addSurvivorTrait, (void **)&originalAddTrait) &&
           installFeatureHook(0x10a2a90, fatiguePrologue, sizeof(fatiguePrologue),
                              setSurvivorFatigue, (void **)&originalSetFatigue) &&
           installFeatureHook(0x109f4c0, woundPrologue, sizeof(woundPrologue),
                              setSurvivorWounds, (void **)&originalSetWounds);
}

static bool playerStateIs(const uint8_t *machine, uint16_t state) {
    const void *playerDefinition = *(const void **)(image + 0x1a8f49c);
    return machine && playerDefinition && *(const void *const *)(machine + 4) == playerDefinition &&
           *(const uint16_t *)(machine + 0x0c) == state;
}

static CombatVector *__cdecl solveMovementLaunch(CombatVector *out, const uint8_t *machine,
    float angle, float gravity, CombatVector target, CombatVector origin) {
    CombatVector *launch = originalMovementLaunch(out, machine, angle, gravity, target, origin);
    // Environmental_jump_forward launches through MovementLaunchToPoint.
    if ((applied & (1u << JumpHeight)) && __builtin_return_address(0) == image + 0x128d7cc &&
        playerStateIs(machine, 447) && *(void *const *)(machine + 8) == controlledActor() &&
        launch->z > 0.0f) {
        launch->z *= sqrtf(jumpHeightMultiplier);
    }
    return launch;
}

static void __thiscall applyPhysicalMovement(void *animated, const MovementTransform *movement,
                                             const MovementTransform *merged) {
    if (applied & ((1u << Speed) | (1u << JumpHeight))) {
        const uint8_t *actor = controlledActor();
        if (actor && *(void *const *)(actor + 0x0c) == *(void **)((uint8_t *)animated + 0x0c) &&
            !occupiedVehicle((void *)actor)) {
            MovementTransform scaled = *merged;
            if ((applied & (1u << Speed)) && *(const unsigned *)((uint8_t *)animated + 0x848) == 1) {
                scaled.translation[0] *= movementMultiplier;
                scaled.translation[1] *= movementMultiplier;
            }
            // Environmental_jump_stand supplies vertical motion through the animation.
            const uint8_t *machine = *(const uint8_t *const *)(actor + 0x1100);
            if ((applied & (1u << JumpHeight)) && playerStateIs(machine, 455))
                scaled.translation[2] *= jumpHeightMultiplier;
            originalPhysicalMovement(animated, movement, &scaled);
            return;
        }
    }
    originalPhysicalMovement(animated, movement, merged);
}

static void __cdecl changeStock(uint32_t resource, float delta) {
    // Stockpile.Ammo through Stockpile.Medicine in the active community.
    uint32_t stockpile = resource & 0xff00ffff;
    if ((applied & (1u << Resources)) && stockpile >= 0x09000001 &&
        stockpile <= 0x09000005 && delta < 0) return;
    originalChangeStock(resource, delta);
}

static void replenishStock(uint64_t now) {
    static uint64_t nextRefill;
    if (!(applied & (1u << Resources))) {
        nextRefill = 0;
        return;
    }
    if (now < nextRefill || !controlledActor()) return;
    nextRefill = now + 10000000ULL;
    ReadStock readStock = (ReadStock)(image + 0xf95640);
    for (uint32_t i = 1; i <= 5; ++i) {
        uint32_t resource = 0x09000000u + i;
        float stock = readStock(resource);
        if (stock < 99.0f) originalChangeStock(resource, 99.0f - stock);
    }
}

static bool localFireMode(void *fireMode) {
    void *weapon = *(void **)((uint8_t *)fireMode + 0x10);
    if (!weapon) return false;
    void *owner = ((WeaponOwner)(image + 0x10e7820))(weapon);
    return owner && owner == controlledActor();
}

static int *unlimitedAmmoField(void) {
    uint8_t *gameVariables = *(uint8_t **)(image + 0x1a1e634);
    return gameVariables ? (int *)(gameVariables + 0x10c) : NULL;
}

static void __thiscall finishReload(void *fireMode, uint32_t reloadArgument) {
    int *unlimited = (applied & (1u << Ammo)) && localFireMode(fireMode) ? unlimitedAmmoField() : NULL;
    int saved = unlimited ? *unlimited : 0;
    // The reload routine uses this flag after it transfers rounds to the magazine.
    if (unlimited) *unlimited = 1;
    // The original tail call consumes one stack argument even though it replaces its value.
    originalFinishReload(fireMode, reloadArgument);
    if (unlimited) *unlimited = saved;
}

static float __thiscall recoil(void *fireMode) {
    return (applied & (1u << Recoil)) && localFireMode(fireMode) ? 0.0f : originalRecoil(fireMode);
}

static float __thiscall recoilLimit(void *fireMode) {
    return (applied & (1u << Recoil)) && localFireMode(fireMode) ? 0.0f : originalRecoilLimit(fireMode);
}

static bool installGameplayHooks(void) {
    static const uint8_t stockPrologue[] = {0x51, 0x53, 0x8b, 0x5c, 0x24, 0x0c};
    static const uint8_t readStockPrologue[] = {0x51, 0x8b, 0x54, 0x24, 0x08};
    static const uint8_t recoilPrologue[] = {0x83, 0xec, 0x0c, 0x53, 0x56, 0x57, 0x8b, 0xf9};
    static const uint8_t recoilLimitPrologue[] = {0x55, 0x8b, 0xec, 0x83, 0xe4, 0xf0};
    static const uint8_t reloadPrologue[] = {0x83, 0xec, 0x24, 0x55, 0x8b, 0xe9, 0x56};
    if (memcmp(image + 0xf94d30, stockPrologue, sizeof(stockPrologue)) ||
        memcmp(image + 0xf95640, readStockPrologue, sizeof(readStockPrologue)) ||
        memcmp(image + 0x122d200, recoilPrologue, sizeof(recoilPrologue)) ||
        memcmp(image + 0x122d4f0, recoilLimitPrologue, sizeof(recoilLimitPrologue)) ||
        memcmp(image + 0x122b640, reloadPrologue, sizeof(reloadPrologue))) return false;
    if (MH_Initialize() != MH_OK) return false;
    if (MH_CreateHook(image + 0xf94d30, changeStock, (void **)&originalChangeStock) == MH_OK &&
        MH_CreateHook(image + 0x122d200, recoil, (void **)&originalRecoil) == MH_OK &&
        MH_CreateHook(image + 0x122d4f0, recoilLimit, (void **)&originalRecoilLimit) == MH_OK &&
        MH_CreateHook(image + 0x122b640, finishReload, (void **)&originalFinishReload) == MH_OK &&
        MH_EnableHook(MH_ALL_HOOKS) == MH_OK) return true;
    MH_Uninitialize();
    return false;
}

static uint64_t utcTicks(void) {
    FILETIME time;
    GetSystemTimeAsFileTime(&time);
    return (((uint64_t)time.dwHighDateTime << 32) | time.dwLowDateTime) + 504911232000000000ULL;
}

static unsigned readSettings(uint64_t now) {
    char buffer[512], nextSession[33];
    unsigned owner, flags;
    unsigned long long expiry;
    int valuesStart = 0;
    DWORD size;
    HANDLE file = CreateFileW(settings, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                              NULL, OPEN_EXISTING, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return 0;
    bool ok = ReadFile(file, buffer, sizeof(buffer) - 1, &size, NULL);
    CloseHandle(file);
    if (!ok || size == sizeof(buffer) - 1) return 0;
    buffer[size] = 0;
    if (sscanf(buffer, "%u|%llu|%u|%32[0-9a-f]|%n", &owner, &expiry, &flags,
               nextSession, &valuesStart) != 4 || !valuesStart || strlen(nextSession) != 32 ||
        owner != pid || expiry <= now || expiry - now > 50000000ULL || (flags & ~All)) return 0;
    const char *cursor = buffer + valuesStart;
    float multiplier = 1.0f, xpMultiplier = 1.0f, jumpMultiplier = 1.0f;
    for (unsigned i = 0; i < Count; ++i) {
        char *end;
        float value = strtof(cursor, &end);
        if (end == cursor || *end != (i + 1 == Count ? '\0' : ',')) return 0;
        if (i == Speed) multiplier = value;
        if (i == Experience) xpMultiplier = value;
        if (i == JumpHeight) jumpMultiplier = value;
        cursor = end + 1;
    }
    if (!(multiplier >= 1.0f && multiplier <= 3.0f) ||
        !(xpMultiplier >= 1.0f && xpMultiplier <= 10.0f) ||
        !(jumpMultiplier >= 1.0f && jumpMultiplier <= 10.0f)) return 0;
    movementMultiplier = multiplier;
    experienceMultiplier = xpMultiplier;
    jumpHeightMultiplier = jumpMultiplier;
    memcpy(session, nextSession, sizeof(session));
    return flags;
}

static void publish(uint64_t now, bool ready) {
    char buffer[128];
    int length = snprintf(buffer, sizeof(buffer), "2|%lu|%llu|%d|%u|%s||0|",
                          pid, (unsigned long long)now, ready, ready ? applied : 0, session);
    HANDLE file = CreateFileW(pending, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return;
    DWORD written;
    bool ok = WriteFile(file, buffer, (DWORD)length, &written, NULL) && written == (DWORD)length;
    CloseHandle(file);
    if (ok) MoveFileExW(pending, status, MOVEFILE_REPLACE_EXISTING);
}

static void *variable(void *console, const char *name) {
    void **methods = *(void ***)console;
    return ((GetVariable)methods[0x54 / 4])(console, name);
}

static float getValue(void *object, bool floating) {
    void **methods = *(void ***)object;
    return floating ? ((GetFloat)methods[3])(object) : ((GetInt)methods[2])(object);
}

static void setValue(void *object, bool floating, float value) {
    void **methods = *(void ***)object;
    if (floating) ((SetFloat)methods[6])(object, value);
    else ((SetInt)methods[5])(object, (int)value);
}

static bool applyPatch(unsigned command, bool enable) {
    bool matched = false;
    for (unsigned i = 0; i < sizeof(patches) / sizeof(patches[0]); ++i) {
        if (patches[i].command != command) continue;
        const uint8_t *address = image + patches[i].offset;
        if (memcmp(address, patches[i].original, patches[i].size) &&
            memcmp(address, patches[i].enabled, patches[i].size)) return false;
        matched = true;
    }
    for (unsigned i = 0; i < sizeof(patches) / sizeof(patches[0]); ++i) {
        if (patches[i].command != command) continue;
        uint8_t *address = image + patches[i].offset;
        size_t size = patches[i].size;
        const uint8_t *after = enable ? patches[i].enabled : patches[i].original;
        if (memcmp(address, after, size) == 0) continue;
        DWORD previous;
        if (!VirtualProtect(address, size, PAGE_EXECUTE_READWRITE, &previous)) return false;
        memcpy(address, after, size);
        FlushInstructionCache(GetCurrentProcess(), address, size);
        VirtualProtect(address, size, previous, &previous);
    }
    return matched;
}

static bool applySettings(unsigned desired) {
    uint8_t *environment = *(uint8_t **)(image + 0x1aed558);
    if (!environment) return false;
    void *console = *(void **)(environment + 0x1c);
    if (!console) return false;

    void *objects[Count] = {0};
    for (unsigned i = 0; i < Count; ++i) {
        if (!variables[i].name) continue;
        objects[i] = variable(console, variables[i].name);
        if (!objects[i]) return false;
    }
    void *engine = *(void **)(environment + 0x24);
    if (!engine) return false;
    void *ignorePlayer = variable(console, "ai_IgnorePlayer");
    static int previousIgnorePlayer;

    for (unsigned i = 0; i < Count; ++i) {
        unsigned bit = 1u << i;
        bool enable = (desired & bit) != 0;
        bool wasEnabled = (applied & bit) != 0;
        if (!enable && !wasEnabled) continue;
        if (i == Stealth && ignorePlayer) {
            if (enable && !wasEnabled) previousIgnorePlayer = (int)getValue(ignorePlayer, false);
            setValue(ignorePlayer, false, enable ? 1 : previousIgnorePlayer);
        }
        if (i == Build || i == Experience || i == VehicleRepair || i == Speed) {
            if (i == VehicleRepair || (i == Build && buildReady) ||
                (i == Experience && experienceReady) || (i == Speed && movementReady))
                applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == ToughNpc || i == GodMode) {
            bool sharedReady = protectionReady && combatHitReady && clientHitReady && killReady &&
                               actorStateReady && grabReady && entityLinkReady && meleeContactReady;
            if (sharedReady && (i == GodMode ? nativeGodReady : allyProtectionReady))
                applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == JumpHeight) {
            if (jumpReady && movementReady) applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == SurvivorImmunity) {
            if (survivorImmunityReady) applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == Resources || i == Ammo || i == Recoil) {
            if (hooksReady) applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == NoReload) {
            // The CryEngine firing code reads the game CVar backing field directly.
            int *unlimited = unlimitedAmmoField();
            if (unlimited) {
                if (enable && !wasEnabled) variables[i].saved = (float)*unlimited;
                *unlimited = enable ? 1 : (int)variables[i].saved;
                applied ^= (applied ^ desired) & bit;
            }
            continue;
        }
        if (i == Weight) {
            if (weightReady && enable != wasEnabled && applyPatch(i, enable)) {
                applied ^= bit;
                uint8_t *character = *(uint8_t **)(image + 0x1a658c8);
                if (character && *(void **)(character + 0xe8) && controlledActor())
                    ((UpdateEncumbrance)(image + 0x10a2720))(character);
            }
            continue;
        }
        if (i == OneHit) {
            if (combatHitReady && clientHitReady && killReady && meleeContactReady) applied ^= (applied ^ desired) & bit;
            continue;
        }
        if (i == Suppressor) {
            if (applyPatch(i, enable)) applied ^= (applied ^ desired) & bit;
            continue;
        }
        // The current container duration overrides sr_searchDurationNormal.
        float *direct = i == Loot ? (float *)(image + 0x1513844) :
                        i == Radio ? (float *)(image + 0x1adef54) : NULL;
        float current = direct ? *direct : getValue(objects[i], variables[i].floating);
        if (enable && !wasEnabled) variables[i].saved = current;
        float target = enable ? variables[i].enabled : variables[i].saved;
        if (direct) *direct = target;
        else if (current != target) setValue(objects[i], variables[i].floating, target);
        bool hourApplied = true;
        if (i == Morning && enable) {
            void **engineMethods = *(void ***)engine;
            void *clock = ((GetTimeOfDay)engineMethods[0x2e0 / 4])(engine);
            hourApplied = clock != NULL;
            if (clock) {
                void **clockMethods = *(void ***)clock;
                ((SetHour)clockMethods[5])(clock, 8.0f, false);
            }
        }
        if (hourApplied && (direct || getValue(objects[i], variables[i].floating) == target)) {
            if (enable) applied |= bit;
            else applied &= ~bit;
        }
    }
    return true;
}

static BOOL WINAPI peekMessage(LPMSG msg, HWND window, UINT first, UINT last, UINT remove) {
    // The main loop calls PeekMessageW before updating the game world.
    if (__builtin_return_address(0) == image + 0xecffc2 ||
        __builtin_return_address(0) == image + 0x1110b67) {
        static ULONGLONG lastPoll;
        ULONGLONG tick = GetTickCount64();
        if (tick - lastPoll >= 100) {
            lastPoll = tick;
            static bool hooksAttempted;
            if (!hooksAttempted) {
                hooksAttempted = true;
                hooksReady = installGameplayHooks();
                protectionReady = installProtectionHooks();
                allyProtectionReady = protectionReady && installAllyProtectionHooks();
                survivorImmunityReady = installSurvivorImmunityHooks();
                static const uint8_t buildPrologue[] = {0x83, 0xec, 0x20, 0x57, 0x8b, 0xf9};
                static const uint8_t xpPrologue[] = {0x51, 0x53, 0x55, 0xff, 0x74, 0x24, 0x10};
                static const uint8_t movementPrologue[] = {0x81, 0xec, 0x00, 0x01, 0x00, 0x00};
                static const uint8_t hitPrologue[] = {0x83, 0xec, 0x24, 0x53, 0x55};
                static const uint8_t clientHitPrologue[] = {0x83, 0xec, 0x14, 0x53, 0x8b, 0xd9};
                static const uint8_t killPrologue[] = {0x83, 0xec, 0x5c, 0xa1, 0x58, 0xd5, 0xee, 0x01};
                static const uint8_t godPrologue[] = {0x57, 0x8b, 0xf9, 0x8b, 0x0d, 0x14, 0xee, 0xe1, 0x01};
                static const uint8_t actorStatePrologue[] = {0x81, 0xec, 0x24, 0x01, 0x00, 0x00};
                static const uint8_t grabPrologue[] = {0x81, 0xec, 0x1c, 0x01, 0x00, 0x00};
                static const uint8_t linkPrologue[] = {0x8b, 0x44, 0x24, 0x04, 0x83, 0xec, 0x14};
                static const uint8_t meleePrologue[] = {0x55, 0x8b, 0xec, 0x83, 0xe4, 0xf0};
                static const uint8_t reactionPrologue[] = {0x83, 0xec, 0x0c, 0xf3, 0x0f, 0x10, 0x4c, 0x24, 0x14};
                static const uint8_t weightPrologue[] = {0x83, 0xec, 0x08, 0x0f, 0x57, 0xc0};
                static const uint8_t vehiclePrologue[] = {0x0f, 0x57, 0xc9, 0x83, 0xec, 0x3c};
                static const uint8_t stimulusPrologue[] = {0x53, 0x8b, 0x5c, 0x24, 0x08, 0x56};
                installFeatureHook(0x3dcb30, vehiclePrologue, sizeof(vehiclePrologue),
                                   hitVehicleComponent, (void **)&originalHitVehicleComponent);
                installFeatureHook(0x58b940, stimulusPrologue, sizeof(stimulusPrologue),
                                   registerStimulus, (void **)&originalRegisterStimulus);
                combatHitReady = installFeatureHook(0x123d1b0, hitPrologue, sizeof(hitPrologue),
                                                    processCombatHit, (void **)&originalCombatHit);
                clientHitReady = installFeatureHook(0x1238e60, clientHitPrologue, sizeof(clientHitPrologue),
                                                    processClientHit, (void **)&originalClientHit);
                killReady = installFeatureHook(0x112a470, killPrologue, sizeof(killPrologue),
                                               killActor, (void **)&originalKillActor);
                nativeGodReady = installFeatureHook(0x1102a90, godPrologue, sizeof(godPrologue),
                                                    actorGodMode, (void **)&originalActorGodMode);
                actorStateReady = installFeatureHook(0x118e990, actorStatePrologue, sizeof(actorStatePrologue),
                                                      tickActorState, (void **)&originalActorState);
                meleeSweepThreadState = TlsAlloc();
                if (meleeSweepThreadState != TLS_OUT_OF_INDEXES &&
                             installFeatureHook(0xf3cf70, reactionPrologue, sizeof(reactionPrologue),
                                                queueZombieReaction, (void **)&originalZombieReaction))
                    installFeatureHook(0xf1a040, meleePrologue, sizeof(meleePrologue),
                                       zombieMeleeSweep, (void **)&originalMeleeSweep);
                static const uint8_t meleeContactPrologue[] = {0x81, 0xec, 0x30, 0x02, 0x00, 0x00};
                meleeContactReady = installFeatureHook(0xf8b670, meleeContactPrologue, sizeof(meleeContactPrologue),
                                                        applyMeleeContact, (void **)&originalMeleeContact);
                grabReady = installFeatureHook(0x10f3250, grabPrologue, sizeof(grabPrologue),
                                               startGrab, (void **)&originalStartGrab);
                entityLinkReady = installFeatureHook(0xf7e4d0, linkPrologue, sizeof(linkPrologue),
                                                     linkActor, (void **)&originalLinkActor);
                static const uint8_t jumpPrologue[] = {0xf3, 0x0f, 0x10, 0x4c, 0x24, 0x10};
                jumpReady = installFeatureHook(0x1291bc0, jumpPrologue, sizeof(jumpPrologue),
                                               solveMovementLaunch, (void **)&originalMovementLaunch);
                movementReady = installFeatureHook(0x474f30, movementPrologue, sizeof(movementPrologue),
                                                   applyPhysicalMovement, (void **)&originalPhysicalMovement);
                weightReady = installFeatureHook(0x11de5b0, weightPrologue, sizeof(weightPrologue),
                                                 inventoryWeight, (void **)&originalInventoryWeight);
                buildReady = installFeatureHook(0x1179270, buildPrologue, sizeof(buildPrologue),
                                                updateFacility, (void **)&originalFacilityUpdate);
                experienceReady = installFeatureHook(0x108cbf0, xpPrologue, sizeof(xpPrologue),
                                                     awardExpertise, (void **)&originalAwardExpertise);
            }
            uint64_t now = utcTicks();
            bool ready = applySettings(readSettings(now));
            if (ready) {
                finishPendingZombieDeaths();
                replenishStock(now);
                repairOccupiedVehicle();
                updateStealthSources();
                cureSurvivors();
            }
            publish(now, ready);
        }
    }
    return originalPeek(msg, window, first, last, remove);
}

static bool verifyExecutable(void) {
    static const uint8_t expected[32] = {
        0x31, 0xf4, 0x21, 0x7c, 0x3f, 0xfa, 0xb3, 0x6f, 0xfd, 0xed, 0xb0, 0x3d, 0xf8, 0x87, 0x1d, 0x95,
        0x03, 0x8e, 0x44, 0xf8, 0xc7, 0x69, 0x6a, 0x34, 0xe5, 0xf2, 0xd5, 0x95, 0xfe, 0x81, 0x90, 0x09
    };
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(NULL, path, MAX_PATH);
    if (!length || length >= MAX_PATH) return false;
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return false;
    BCRYPT_ALG_HANDLE algorithm = NULL;
    BCRYPT_HASH_HANDLE hash = NULL;
    uint8_t buffer[16384], digest[32];
    DWORD bytes;
    bool ok = false;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, NULL, 0) < 0 ||
        BCryptCreateHash(algorithm, &hash, NULL, 0, NULL, 0, 0) < 0) goto cleanup;
    do {
        if (!ReadFile(file, buffer, sizeof(buffer), &bytes, NULL) ||
            (bytes && BCryptHashData(hash, buffer, bytes, 0) < 0)) goto cleanup;
    } while (bytes);
    ok = BCryptFinishHash(hash, digest, sizeof(digest), 0) >= 0 &&
         memcmp(digest, expected, sizeof(expected)) == 0;
cleanup:
    if (hash) BCryptDestroyHash(hash);
    if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
    CloseHandle(file);
    return ok;
}

static DWORD WINAPI initialize(void *unused) {
    (void)unused;
    HMODULE runtime;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                            (LPCWSTR)&initialize, &runtime) || !verifyExecutable()) return 0;
    image = (uint8_t *)GetModuleHandleW(NULL);
    pid = GetCurrentProcessId();
    wchar_t folder[MAX_PATH];
    DWORD length = GetTempPathW(MAX_PATH, folder);
    if (!length || length >= MAX_PATH - 64) return 0;
    wcscat(folder, L"TrainerHub-329430");
    if (!CreateDirectoryW(folder, NULL) && GetLastError() != ERROR_ALREADY_EXISTS) return 0;
    swprintf(settings, MAX_PATH, L"%ls\\settings.txt", folder);
    swprintf(status, MAX_PATH, L"%ls\\status.txt", folder);
    swprintf(pending, MAX_PATH, L"%ls\\status-%lu.tmp", folder, pid);

    PeekMessageFn *slot = (PeekMessageFn *)(image + 0x131b764);
    originalPeek = (PeekMessageFn)GetProcAddress(GetModuleHandleW(L"user32.dll"), "PeekMessageW");
    // Steam restores the original imports before entering the game's main loop.
    for (unsigned attempt = 0; attempt < 300 && *slot != originalPeek; ++attempt) Sleep(100);
    if (!originalPeek || *slot != originalPeek) return 0;
    DWORD previous;
    if (!VirtualProtect(slot, sizeof(*slot), PAGE_READWRITE, &previous)) return 0;
    InterlockedCompareExchangePointer((void *volatile *)slot, (void *)peekMessage, (void *)originalPeek);
    VirtualProtect(slot, sizeof(*slot), previous, &previous);
    return 0;
}

__declspec(dllexport) void ByteTrainerRuntime(void) {}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved) {
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        HANDLE thread = CreateThread(NULL, 0, initialize, NULL, 0, NULL);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
