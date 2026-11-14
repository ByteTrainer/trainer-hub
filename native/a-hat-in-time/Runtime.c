#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include "BuildLayout.h"

typedef struct { uintptr_t data; int count, capacity; } Array;
static uintptr_t base;
static HANDLE process;
static wchar_t settings[MAX_PATH], resource[MAX_PATH], status[MAX_PATH], temporary[MAX_PATH];

static int readValue(uintptr_t address, void *output, size_t size)
{
    SIZE_T read;
    return ReadProcessMemory(process, (void *)address, output, size, &read) && read == size;
}

static uintptr_t pointer(uintptr_t address)
{
    uintptr_t value = 0;
    readValue(address, &value, sizeof(value));
    return value;
}

static int named(Array names, uintptr_t object, const char *expected)
{
    int index;
    char text[64] = {0};
    size_t length = strlen(expected) + 1;
    if (!readValue(object + HatObjectName, &index, sizeof(index)) || index < 0 || index >= names.count)
        return 0;
    uintptr_t entry = pointer(names.data + (size_t)index * sizeof(uintptr_t));
    return entry && readValue(entry + HatNameText, text, length) && !memcmp(text, expected, length);
}

static uint64_t ticks(void)
{
    FILETIME time;
    GetSystemTimeAsFileTime(&time);
    return ((uint64_t)time.dwHighDateTime << 32 | time.dwLowDateTime) + 504911232000000000ULL;
}

typedef struct { uintptr_t owner; float baseline, applied; } Override;

static void restore(Override *state, uintptr_t owner, size_t offset)
{
    float current;
    if (state->owner && state->owner == owner &&
        readValue(owner + offset, &current, sizeof(current)) && current == state->applied)
        WriteProcessMemory(process, (void *)(owner + offset), &state->baseline, sizeof(float), NULL);
    state->owner = 0;
}

static void scale(Override *state, uintptr_t owner, size_t offset, float multiplier)
{
    float current;
    if (multiplier == 1 || !owner) { restore(state, owner, offset); return; }
    if (!readValue(owner + offset, &current, sizeof(current)) || !isfinite(current) || current <= 0) return;
    if (state->owner != owner || current != state->applied) state->baseline = current;
    float value = state->baseline * multiplier;
    if (WriteProcessMemory(process, (void *)(owner + offset), &value, sizeof(value), NULL)) {
        state->owner = owner;
        state->applied = value;
    }
}

typedef struct { uintptr_t weapon; int original, applied; } WeaponOverride;

static void scaleWeapon(WeaponOverride *state, uintptr_t weapon, float multiplier)
{
    int current;
    if (state->weapon && state->weapon != weapon) state->weapon = 0;
    if (!weapon || !readValue(weapon + HatWeaponDamage, &current, sizeof(current))) return;
    if (multiplier == 1) {
        if (state->weapon && current == state->applied)
            WriteProcessMemory(process, (void *)(weapon + HatWeaponDamage), &state->original, sizeof(current), NULL);
        state->weapon = 0;
        return;
    }
    if (current <= 0 || current > 10000) return;
    int original = state->weapon && current == state->applied ? state->original : current;
    int damage = (int)(original * multiplier + 0.5f);
    if (damage != current && WriteProcessMemory(process, (void *)(weapon + HatWeaponDamage),
        &damage, sizeof(damage), NULL)) *state = (WeaponOverride){weapon, original, damage};
}

static int hasClass(Array names, uintptr_t object, const char *name)
{
    uintptr_t type = pointer(object + HatObjectClass);
    for (unsigned depth = 0; type && depth < 24; ++depth) {
        if (named(names, type, name)) return 1;
        type = pointer(type + HatClassSuper);
    }
    return 0;
}

enum { ObjectBatch = 128, ScanBudgetMs = 12 };

static uintptr_t findPlayer(Array names, Array objects, int *cursor)
{
    if (*cursor <= 0 || *cursor > objects.count) *cursor = objects.count;
    ULONGLONG deadline = GetTickCount64() + ScanBudgetMs;
    uintptr_t batch[ObjectBatch];
    do {
        int end = *cursor, start = end > ObjectBatch ? end - ObjectBatch : 0;
        *cursor = start;
        if (!readValue(objects.data + (size_t)start * sizeof(uintptr_t), batch,
            (size_t)(end - start) * sizeof(uintptr_t))) continue;
        for (int i = end - start - 1; i >= 0; --i) {
            uintptr_t object = batch[i];
            if (object && pointer(object + HatControllerPawn) &&
                named(names, pointer(object + 0x4e4), "LocalPlayer") &&
                hasClass(names, object, "Hat_PlayerController")) {
                *cursor = 0;
                return object;
            }
        }
    } while (*cursor > 0 && GetTickCount64() < deadline);
    return 0;
}

static void setBit(uintptr_t address, unsigned bit, int enabled)
{
    unsigned value;
    if (!readValue(address, &value, sizeof(value))) return;
    unsigned next = enabled ? value | bit : value & ~bit;
    if (next != value) WriteProcessMemory(process, (void *)address, &next, sizeof(next), NULL);
}

enum { NoClip = 32768, CollisionMask = 0x160, PhysicsFlying = 4, PhysicsFalling = 2 };

typedef struct {
    uintptr_t pawn, world;
    unsigned collision;
    unsigned char physics;
    int restorePhysics;
} NoClipState;

static void updateNoClip(NoClipState *state, uintptr_t controller, uintptr_t pawn, uintptr_t world, int enabled, DWORD pid)
{
    if (state->pawn && (!enabled || state->pawn != pawn || state->world != world)) {
        if (state->pawn == pawn && state->world == world &&
            pointer(controller + HatControllerPawn) == pawn &&
            pointer(pawn + HatActorWorld) == world) {
            unsigned collision;
            if (readValue(pawn + HatActorCollision, &collision, sizeof(collision))) {
                collision = (collision & ~CollisionMask) | state->collision;
                WriteProcessMemory(process, (void *)(pawn + HatActorCollision), &collision, sizeof(collision), NULL);
            }
            unsigned char physics;
            if (state->restorePhysics && readValue(pawn + HatActorPhysics, &physics, 1) && physics == PhysicsFlying) {
                // Let gravity settle the character after leaving flight in midair.
                physics = state->physics == 1 ? PhysicsFalling : state->physics;
                WriteProcessMemory(process, (void *)(pawn + HatActorPhysics), &physics, 1, NULL);
            }
        }
        *state = (NoClipState){0};
    }
    unsigned collision;
    unsigned char physics;
    if (!enabled || pointer(pawn + HatActorWorld) != world ||
        !readValue(pawn + HatActorPhysics, &physics, 1) ||
        !readValue(pawn + HatActorCollision, &collision, sizeof(collision))) return;
    if (!state->pawn) {
        // Wait until after cutscenes and other scripted movement.
        if (physics < 1 || physics > PhysicsFlying) return;
        *state = (NoClipState){pawn, world, collision & CollisionMask, physics, 0};
    }
    if (physics < 1 || physics > PhysicsFlying) return;
    if (physics != PhysicsFlying) {
        unsigned char flying = PhysicsFlying;
        if (!WriteProcessMemory(process, (void *)(pawn + HatActorPhysics), &flying, 1, NULL)) return;
        state->restorePhysics = 1;
    }
    if (collision & CollisionMask) {
        collision &= ~CollisionMask;
        WriteProcessMemory(process, (void *)(pawn + HatActorCollision), &collision, sizeof(collision), NULL);
    }
    DWORD foregroundPid = 0;
    GetWindowThreadProcessId(GetForegroundWindow(), &foregroundPid);
    if (foregroundPid != pid) return;
    int vertical = ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0) -
        ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0);
    if (vertical) {
        // Change only vertical velocity so ordinary movement can still steer the character.
        float speed = vertical * 700.0f;
        WriteProcessMemory(process, (void *)(pawn + HatActorVelocity + 2 * sizeof(float)), &speed, sizeof(speed), NULL);
    }

}

typedef struct {
    const char *owner, *function;
    int length, offset;
    unsigned char expected[4], replacement[2];
    size_t size;
    uintptr_t address;
    int applied;
} GodScript;

static void updateGodScripts(Array names, Array objects, int enabled)
{
    // Preserve parameter setup before returning from the two handlers.
    static GodScript scripts[] = {
        {"Hat_Player", "CanTakeDamage", 379, 2, {0x07, 0x37, 0x00, 0x82}, {0x04, 0x28}, 2, 0, 0},
        {"Hat_StatusEffect_FallFlatDeath", "OnLanded", 300, 1, {0x07, 0x29, 0x01, 0x82}, {0x04, 0x0b}, 2, 0, 0},
        {"Hat_Player", "FellOutOfWorld", 120, 0, {0x07, 0x75, 0x00, 0x9a}, {0x04, 0x0b}, 2, 0, 0}
    };
    static int cursor, found;
    if (enabled && found < 3 && objects.data && objects.count > 0 && objects.count < 1000000) {
        if (cursor <= 0 || cursor > objects.count) cursor = objects.count;
        ULONGLONG deadline = GetTickCount64() + ScanBudgetMs;
        uintptr_t batch[ObjectBatch];
        while (cursor > 0 && found < 3 && GetTickCount64() < deadline) {
            int end = cursor, start = end > ObjectBatch ? end - ObjectBatch : 0;
            cursor = start;
            if (!readValue(objects.data + (size_t)start * sizeof(uintptr_t), batch,
                (size_t)(end - start) * sizeof(uintptr_t))) continue;
            for (int i = end - start - 1; i >= 0; --i) {
                uintptr_t object = batch[i];
                if (!object || !named(names, pointer(object + HatObjectClass), "Function")) continue;
                for (size_t j = 0; j < 3; ++j) {
                    GodScript *script = &scripts[j];
                    if (script->address || !named(names, object, script->function) ||
                        !named(names, pointer(object + HatObjectOuter), script->owner)) continue;
                    uintptr_t bytes = pointer(object + 0x8c);
                    int length;
                    unsigned char original[4];
                    if (bytes && readValue(object + 0x94, &length, sizeof(length)) && length == script->length &&
                        readValue(bytes + script->offset, original, sizeof(original)) &&
                        !memcmp(original, script->expected, sizeof(original))) {
                        script->address = bytes + script->offset;
                        ++found;
                    }
                }
            }
        }
    }
    for (size_t i = 0; i < 3; ++i) {
        GodScript *script = &scripts[i];
        if (!script->address || script->applied == enabled) continue;
        unsigned char current[2];
        const unsigned char *expected = enabled ? script->expected : script->replacement;
        const unsigned char *replacement = enabled ? script->replacement : script->expected;
        if (readValue(script->address, current, script->size) && !memcmp(current, expected, script->size) &&
            WriteProcessMemory(process, (void *)script->address, replacement, script->size, NULL))
            script->applied = enabled;
    }
}

typedef struct { uintptr_t actor; int index; float original; } GuardOverride;
static GuardOverride guards[512];
static size_t guardCount;

static void restoreGuards(Array objects, uintptr_t world)
{
    for (size_t i = 0; i < guardCount; ++i) {
        GuardOverride guard = guards[i];
        if (guard.index >= objects.count ||
            pointer(objects.data + (size_t)guard.index * sizeof(uintptr_t)) != guard.actor ||
            pointer(guard.actor + HatActorWorld) != world) continue;
        float current;
        if (readValue(guard.actor + HatGuardDetectMultiplier, &current, sizeof(current)) && current == 0)
            WriteProcessMemory(process, (void *)(guard.actor + HatGuardDetectMultiplier),
                &guard.original, sizeof(guard.original), NULL);
    }
    guardCount = 0;
}

static void updateEnemies(Array names, Array objects, uintptr_t world, unsigned mask, int *cursor)
{
    if (!objects.data || objects.count <= 0 || objects.count >= 1000000) return;
    const float clear = 0;
    if (*cursor >= objects.count) *cursor = 0;
    ULONGLONG deadline = GetTickCount64() + ScanBudgetMs;
    uintptr_t batch[ObjectBatch];
    do {
      int start = *cursor, count = objects.count - start;
      if (count > ObjectBatch) count = ObjectBatch;
      *cursor += count;
      if (!readValue(objects.data + (size_t)start * sizeof(uintptr_t), batch,
          (size_t)count * sizeof(uintptr_t))) continue;
      for (int entry = 0; entry < count; ++entry) {
        int i = start + entry;
        uintptr_t actor = batch[entry];
        if (!actor || pointer(actor + HatActorWorld) != world) continue;
        if ((mask & 64) && hasClass(names, actor, "Hat_Enemy")) {
            int health, one = 1;
            if (readValue(actor + HatPawnHealth, &health, sizeof(health)) && health > 1)
                WriteProcessMemory(process, (void *)(actor + HatPawnHealth), &one, sizeof(one), NULL);
        }
        if (!(mask & 32) || !hasClass(names, actor, "Hat_Enemy_SecurityGuard_Base")) continue;
        float rate;
        if (guardCount < sizeof(guards) / sizeof(guards[0]) &&
            readValue(actor + HatGuardDetectMultiplier, &rate, sizeof(rate)) && isfinite(rate) && rate > 0) {
            size_t slot = 0;
            while (slot < guardCount && guards[slot].actor != actor) ++slot;
            if (WriteProcessMemory(process, (void *)(actor + HatGuardDetectMultiplier), &clear, sizeof(clear), NULL)) {
                guards[slot] = (GuardOverride){actor, i, rate};
                if (slot == guardCount) ++guardCount;
            }
        }
        float alert;
        if (readValue(actor + HatGuardAlertness, &alert, sizeof(alert)) && isfinite(alert) && alert > 0)
            WriteProcessMemory(process, (void *)(actor + HatGuardAlertness), &clear, sizeof(clear), NULL);
      }
    } while (*cursor < objects.count && GetTickCount64() < deadline);
}

static uintptr_t findSave(Array names, Array objects)
{
    if (!objects.data || objects.count <= 0 || objects.count >= 1000000) return 0;
    for (int i = objects.count - 1; i >= 0; --i) {
        uintptr_t object = pointer(objects.data + (size_t)i * sizeof(uintptr_t));
        if (!object || !hasClass(names, object, "Hat_SaveManager") ||
            named(names, object, "Default__Hat_SaveManager")) continue;
        uintptr_t save = pointer(object + 0x68);
        if (save && hasClass(names, save, "Hat_SaveGame")) return save;
    }
    return 0;
}

#include "Tracker.h"

typedef struct { uintptr_t actor; int index; } TrackerActor;
typedef struct {
    uintptr_t controller, pawn, world;
    unsigned flags;
    TrackerActor actors[2];
} TrackerState;

static SRWLOCK trackerLock = SRWLOCK_INIT;
static TrackerState trackerState;

static int targetPosition(uintptr_t actor, uintptr_t world, Position origin, Target *target)
{
    unsigned state;
    Position point;
    if (!actor || pointer(actor + HatActorWorld) != world ||
        !readValue(actor + 0xfc, &state, sizeof(state)) || (state & 10) ||
        !readValue(actor + HatActorLocation, &point, sizeof(point))) return 0;
    Position delta = {point.x-origin.x, point.y-origin.y, point.z-origin.z};
    float distance2 = delta.x*delta.x + delta.y*delta.y + delta.z*delta.z;
    if (!isfinite(distance2)) return 0;
    *target = (Target){delta, distance2};
    return 1;
}

typedef struct {
    int cursor;
    TrackerActor targets[2];
    float nearest[2];
    Position origin;
} TargetScan;

static int findTargets(Array names, Array objects, uintptr_t world, TargetScan *scan, unsigned flags)
{
    uintptr_t actors[ObjectBatch];
    ULONGLONG deadline = GetTickCount64() + ScanBudgetMs;
    while (scan->cursor < objects.count && GetTickCount64() < deadline) {
        int offset = scan->cursor, count = objects.count - offset;
        if (count > ObjectBatch) count = ObjectBatch;
        scan->cursor += count;
        if (!readValue(objects.data + (size_t)offset * sizeof(uintptr_t), actors,
            (size_t)count * sizeof(uintptr_t))) continue;
        for (int i = 0; i < count; ++i) {
            uintptr_t actor = actors[i];
            unsigned state;
            if (!actor || pointer(actor + HatActorWorld) != world ||
                !readValue(actor + 0xfc, &state, sizeof(state)) || (state & 10)) continue;
            int rift = (flags & 4096) && (hasClass(names, actor, "Hat_TimeRiftGate_Base") ||
                hasClass(names, actor, "Hat_TimeRiftPortal"));
            if (!rift && (!(flags & 2048) ||
                !(hasClass(names, actor, "Hat_Collectible_Important") ||
                  hasClass(names, actor, "Hat_Collectible_HatPart") ||
                  hasClass(names, actor, "Hat_Collectible_Decoration") ||
                  hasClass(names, actor, "Hat_Collectible_StoryBookPage") ||
                  hasClass(names, actor, "Hat_Collectible_RouletteToken") ||
                  hasClass(names, actor, "Hat_Collectible_TimePieceShard_Base") ||
                  hasClass(names, actor, "Hat_TimeObject_Base")))) continue;
            Target target;
            if (targetPosition(actor, world, scan->origin, &target) && target.distance2 < scan->nearest[rift]) {
                scan->nearest[rift] = target.distance2;
                scan->targets[rift] = (TrackerActor){actor, offset + i};
            }
        }
    }
    return scan->cursor >= objects.count;
}

static DWORD WINAPI renderTracker(void *unused)
{
    (void)unused;
    DWORD pid = GetCurrentProcessId();
    for (;;) {
        AcquireSRWLockShared(&trackerLock);
        TrackerState frame = trackerState;
        ReleaseSRWLockShared(&trackerLock);

        Position origin;
        int yaw = 0;
        Target targets[2] = {{.distance2 = INFINITY}, {.distance2 = INFINITY}};
        if (frame.flags &&
            pointer(frame.controller + HatControllerPawn) == frame.pawn &&
            pointer(frame.controller + HatActorWorld) == frame.world &&
            readValue(frame.pawn + HatActorLocation, &origin, sizeof(origin))) {
            Array objects = {0};
            readValue(base + HatObjectsRva, &objects, sizeof(objects));
            readValue(frame.controller + 0x90, &yaw, sizeof(yaw));
            for (int i = 0; i < 2; ++i) {
                TrackerActor target = frame.actors[i];
                if (target.actor && target.index < objects.count &&
                    pointer(objects.data + (size_t)target.index * sizeof(uintptr_t)) == target.actor)
                    targetPosition(target.actor, frame.world, origin, &targets[i]);
            }
        } else {
            frame.flags = 0;
        }
        showTracker(pid, frame.flags, targets, yaw);
        Sleep(frame.flags ? 16 : 50);
    }
}

static DWORD WINAPI run(void *unused)
{
    (void)unused;
    base = (uintptr_t)GetModuleHandleW(NULL);
    process = GetCurrentProcess();
    wchar_t folder[MAX_PATH];
    DWORD length = GetTempPathW(MAX_PATH, folder);
    if (!length || length >= MAX_PATH - 65) return 0;
    wcscat(folder, L"TrainerHub-253230");
    CreateDirectoryW(folder, NULL);
    unsigned long pid = GetCurrentProcessId();
    swprintf(settings, MAX_PATH, L"%ls\\settings.txt", folder);
    swprintf(status, MAX_PATH, L"%ls\\status.txt", folder);
    swprintf(temporary, MAX_PATH, L"%ls\\status-%lu.tmp", folder, pid);
    WeaponOverride weaponDamage = {0};
    swprintf(resource, MAX_PATH, L"%ls\\resource.txt", folder);
    char ack[33] = "", appliedSession[33] = "";
    const char *actionError = "";
    int applied = 0;
    NoClipState noClip = {0};
    Override jump = {0}, movement = {0}, time = {0};
    uintptr_t controller = 0;
    int playerCursor = 0, enemyCursor = 0;
    uintptr_t enemyWorld = 0;
    unsigned enemyFlags = 0;
    uintptr_t godController = 0, stealthPawn = 0;
    int originalGodMode = 0;
    int originalStealth = 0;
    TrackerActor targets[2] = {0};
    HANDLE renderer = CreateThread(NULL, 0, renderTracker, NULL, 0, NULL);
    if (renderer) CloseHandle(renderer);
    uintptr_t trackedWorld = 0;
    unsigned trackedFlags = 0, nextTrackScan = 0;
    TargetScan targetScan = {0};
    uintptr_t trackedPawn = 0;
    int teleportRequested = 0;
    for (;;) {
        Array names = {0}, objects = {0};
        readValue(base + HatNamesRva, &names, sizeof(names));
        readValue(base + HatObjectsRva, &objects, sizeof(objects));
        int validObjects = objects.data && objects.count > 0 && objects.count < 1000000;
        if (controller && (!pointer(controller + HatControllerPawn) ||
            !named(names, pointer(controller + 0x4e4), "LocalPlayer") ||
            !hasClass(names, controller, "Hat_PlayerController"))) controller = 0;
        if (!controller && validObjects && names.data && names.count > 0 && names.count < 1000000)
            controller = findPlayer(names, objects, &playerCursor);
        uintptr_t pawn = pointer(controller + HatControllerPawn);
        uintptr_t world = pointer(controller + HatActorWorld);
        unsigned char mode = 255;
        int health = 0, maximum = 0;
        int ready = pawn && world && readValue(world + HatWorldNetMode, &mode, 1) && mode == 0 &&
            readValue(pawn + HatPawnHealth, &health, sizeof(health)) && health > 0;
        unsigned mask = 0, candidate = 0;
        unsigned long owner;
        unsigned long long expiry;
        char session[33] = "", incoming[33], line[256];
        float j = 1, m = 1, t = 1, damage = 1;
        FILE *input = _wfopen(settings, L"rb");
        uint64_t now = ticks();
        if (input) {
            if (fgets(line, sizeof(line), input) &&
                sscanf(line, "%lu|%llu|%u|%32[0-9a-f]|1,%f,%f,%f,1,1,1,%f,1,1,1,1,1", &owner, &expiry, &candidate, incoming, &j, &m, &t, &damage) == 8 &&
                owner == pid && expiry > now && expiry - now <= 50000000 && !(candidate & ~(30975u | NoClip)) &&
                j >= 0.5f && j <= 3 && m >= 0.5f && m <= 3 && t >= 0.5f && t <= 3 && damage >= 1 && damage <= 10 && ready) {
                mask = candidate;
                strcpy(session, incoming);
            }
            fclose(input);
        }
        updateNoClip(&noClip, controller, pawn, world, (mask & NoClip) != 0, pid);
        updateGodScripts(names, objects, (mask & 8192) != 0);
        if (godController && (godController != controller || !(mask & 8192))) {
            if (hasClass(names, godController, "Hat_PlayerController") &&
                named(names, pointer(godController + 0x4e4), "LocalPlayer"))
                setBit(godController + HatControllerGodMode, 2, originalGodMode);
            godController = 0;
        }
        if (mask & 8192) {
            unsigned flags;
            if (!godController && readValue(controller + HatControllerGodMode, &flags, sizeof(flags))) {
                godController = controller;
                originalGodMode = (flags & 2) != 0;
            }
            if (godController) setBit(godController + HatControllerGodMode, 2, 1);
        }
        if (ready && (mask & 1) && readValue(pawn + HatPawnHealthMax, &maximum, sizeof(maximum)) &&
            maximum > health && maximum <= 100)
            WriteProcessMemory(process, (void *)(pawn + HatPawnHealth), &maximum, sizeof(maximum), NULL);
        scale(&jump, ready ? pawn : 0, HatPawnJumpHeight, mask & 2 ? j : 1);
        scale(&movement, ready ? pawn : 0, HatPawnMovementSpeedModifier, mask & 4 ? m : 1);
        scale(&time, ready ? world : 0, HatWorldTimeDilation, mask & 8 ? t : 1);
        int hatKid = ready && named(names, pointer(pawn + HatObjectClass), "Hat_Player_HatKid");
        if (hatKid && (mask & 16)) setBit(pawn + HatPawnJumpFlags, 0x200, 1);
        if (stealthPawn && (!(mask & 32) || pawn != stealthPawn || !hatKid)) {
            if (pawn == stealthPawn) setBit(pawn + HatPawnInvisibleToAI, 1, originalStealth);
            stealthPawn = 0;
        }
        if (hatKid && (mask & 32)) {
            unsigned value;
            if (!stealthPawn && readValue(pawn + HatPawnInvisibleToAI, &value, sizeof(value))) {
                originalStealth = (value & 1) != 0;
                stealthPawn = pawn;
            }
            if (stealthPawn) setBit(pawn + HatPawnInvisibleToAI, 1, 1);

        }
        if (!(mask & 32) && guardCount) restoreGuards(objects, world);
        unsigned activeEnemies = mask & 96;
        if (enemyWorld != world || enemyFlags != activeEnemies) enemyCursor = 0;
        enemyWorld = world;
        enemyFlags = activeEnemies;
        if (activeEnemies) updateEnemies(names, objects, world, mask, &enemyCursor);
        uintptr_t weapon = pawn ? pointer(pawn + HatPawnWeapon) : 0;
        if (weapon && !hasClass(names, weapon, "Hat_Weapon")) weapon = 0;
        scaleWeapon(&weaponDamage, weapon, ready && (mask & 128) ? damage : 1);
        unsigned trackerFlags = mask & 6144;
        unsigned scanFlags = session[0] ? trackerFlags | 2048 : 0;
        Position origin;
        if (!validObjects || !scanFlags || !readValue(pawn + HatActorLocation, &origin, sizeof(origin)))
            scanFlags = trackerFlags = 0;
        DWORD moment = GetTickCount();
        if (world != trackedWorld || pawn != trackedPawn || scanFlags != trackedFlags) {
            targets[0] = targets[1] = (TrackerActor){0};
            targetScan.cursor = 0;
            trackedWorld = world;
            trackedPawn = pawn;
            nextTrackScan = moment;
        }
        if (!(mask & 16384)) teleportRequested = 0;
        if (scanFlags && (mask & 16384) && !teleportRequested) {
            TrackerActor target = targets[0];
            Target current;
            unsigned char currentMode = 255;
            if (target.actor && target.index < objects.count &&
                pointer(objects.data + (size_t)target.index * sizeof(uintptr_t)) == target.actor &&
                pointer(controller + HatControllerPawn) == pawn &&
                pointer(controller + HatActorWorld) == world &&
                readValue(world + HatWorldNetMode, &currentMode, 1) && currentMode == 0 &&
                targetPosition(target.actor, world, origin, &current)) {
                Position destination = {origin.x + current.delta.x, origin.y + current.delta.y,
                    origin.z + current.delta.z + 80};
                if (WriteProcessMemory(process, (void *)(pawn + HatActorLocation), &destination, sizeof(destination), NULL)) {
                    teleportRequested = 1;
                    targets[0] = (TrackerActor){0};
                    targetScan.cursor = 0;
                    origin = destination;
                }
            }
            if (!teleportRequested) targets[0] = (TrackerActor){0};
            nextTrackScan = moment;
        }
        if (scanFlags && (LONG)(moment - nextTrackScan) >= 0) {
            if (!targetScan.cursor)
                targetScan = (TargetScan){.nearest = {INFINITY, INFINITY}, .origin = origin};
            if (findTargets(names, objects, world, &targetScan, scanFlags)) {
                targets[0] = targetScan.targets[0];
                targets[1] = targetScan.targets[1];
                targetScan.cursor = 0;
                nextTrackScan = GetTickCount() + 350;
            }
        }
        AcquireSRWLockExclusive(&trackerLock);
        trackerState = (TrackerState){controller, pawn, world, trackerFlags, {targets[0], targets[1]}};
        ReleaseSRWLockExclusive(&trackerLock);
        trackedFlags = scanFlags;
        if (ready && session[0]) {
            if (strcmp(appliedSession, session)) {
                strcpy(appliedSession, session);
                ack[0] = 0;
                applied = 0;
                actionError = "";
            }
            FILE *request = _wfopen(resource, L"rb");
            if (request) {
                char token[33], nonce[33], extra;
                int choice, amount;
                if (fgets(line, sizeof(line), request) &&
                    sscanf(line, "%lu|%llu|%32[0-9a-f]|%32[0-9a-f]|%d|%d %c",
                        &owner, &expiry, token, nonce, &choice, &amount, &extra) == 6 &&
                    owner == pid && expiry > now && expiry - now <= 50000000 &&
                    !strcmp(token, session) && strlen(nonce) == 32 && strcmp(nonce, ack) && (choice == 1 || choice == 2)) {
                    strcpy(ack, nonce);
                    actionError = "Could not update the current resource balance.";
                    uintptr_t save = findSave(names, objects);
                    size_t offset = choice == 1 ? 0xf8 : 0xf0;
                    int balance;
                    if (save && amount > 0 && amount <= 1000000 &&
                        readValue(save + offset, &balance, sizeof(balance)) && balance >= 0 &&
                        balance <= 1000000000 - amount) {
                        balance += amount;
                        if (WriteProcessMemory(process, (void *)(save + offset), &balance, sizeof(balance), NULL)) {
                            applied = balance;
                            actionError = "";
                        }
                    }
                }
                fclose(request);
            }
        }
        FILE *output = _wfopen(temporary, L"wb");
        if (output) {
            fprintf(output, "2|%lu|%llu|%d|%u|%s|%s|%d|%s", pid, (unsigned long long)ticks(), ready, mask, session, ack, applied, actionError);
            if (!fclose(output)) MoveFileExW(temporary, status, MOVEFILE_REPLACE_EXISTING);
        }
        Sleep(100);
    }
}

__declspec(dllexport) void ByteTrainerRuntime(void) {}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        HANDLE worker = CreateThread(NULL, 0, run, NULL, 0, NULL);
        if (worker) CloseHandle(worker);
    }
    return TRUE;
}
