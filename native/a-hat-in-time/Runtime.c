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

static uintptr_t findPlayer(Array names, Array objects)
{
    for (int i = objects.count - 1; i >= 0; --i) {
        uintptr_t object = pointer(objects.data + (size_t)i * sizeof(uintptr_t));
        if (object && pointer(object + HatControllerPawn) && hasClass(names, object, "Hat_PlayerController") &&
            named(names, pointer(object + 0x4e4), "LocalPlayer"))
            return object;
    }
    return 0;
}

static void setBit(uintptr_t address, unsigned bit, int enabled)
{
    unsigned value;
    if (!readValue(address, &value, sizeof(value))) return;
    unsigned next = enabled ? value | bit : value & ~bit;
    if (next != value) WriteProcessMemory(process, (void *)address, &next, sizeof(next), NULL);
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

static void updateEnemies(Array names, Array objects, uintptr_t world, unsigned mask)
{
    if (!objects.data || objects.count <= 0 || objects.count >= 1000000) return;
    const float clear = 0;
    for (int i = objects.count - 1; i >= 0; --i) {
        uintptr_t actor = pointer(objects.data + (size_t)i * sizeof(uintptr_t));
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

static void findTargets(Array names, Array objects, uintptr_t world, Position origin, TrackerActor targets[2], unsigned flags)
{
    targets[0] = targets[1] = (TrackerActor){0};
    float nearest[2] = {INFINITY, INFINITY};
    uintptr_t actors[512];
    for (int offset = 0; offset < objects.count; offset += 512) {
        int count = objects.count - offset;
        if (count > 512) count = 512;
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
            if (targetPosition(actor, world, origin, &target) && target.distance2 < nearest[rift]) {
                nearest[rift] = target.distance2;
                targets[rift] = (TrackerActor){actor, offset + i};
            }
        }
    }
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
    Override jump = {0}, movement = {0}, time = {0};
    uintptr_t controller = 0;
    unsigned scans = 0;
    uintptr_t stealthPawn = 0;
    int originalStealth = 0;
    TrackerActor targets[2] = {0};
    HANDLE renderer = CreateThread(NULL, 0, renderTracker, NULL, 0, NULL);
    if (renderer) CloseHandle(renderer);
    uintptr_t trackedWorld = 0;
    unsigned trackedFlags = 0, nextTrackScan = 0;
    for (;;) {
        Array names = {0}, objects = {0};
        readValue(base + HatNamesRva, &names, sizeof(names));
        readValue(base + HatObjectsRva, &objects, sizeof(objects));
        if (names.count > 0 && names.count < 1000000 && objects.count > 0 && objects.count < 1000000 &&
            (++scans % 10 == 1 || !controller)) controller = findPlayer(names, objects);
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
                owner == pid && expiry > now && expiry - now <= 50000000 && !(candidate & ~6399u) &&
                j >= 0.5f && j <= 3 && m >= 0.5f && m <= 3 && t >= 0.5f && t <= 3 && damage >= 1 && damage <= 10 && ready) {
                mask = candidate;
                strcpy(session, incoming);
            }
            fclose(input);
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
        if (ready && (mask & 96)) updateEnemies(names, objects, world, mask);
        uintptr_t weapon = pawn ? pointer(pawn + HatPawnWeapon) : 0;
        if (weapon && !hasClass(names, weapon, "Hat_Weapon")) weapon = 0;
        scaleWeapon(&weaponDamage, weapon, ready && (mask & 128) ? damage : 1);
        unsigned trackerFlags = mask & 6144;
        Position origin;
        if (trackerFlags && objects.count > 0 && objects.count < 1000000 &&
            readValue(pawn + HatActorLocation, &origin, sizeof(origin))) {
            DWORD moment = GetTickCount();
            if (world != trackedWorld || trackerFlags != trackedFlags || (LONG)(moment - nextTrackScan) >= 0) {
                findTargets(names, objects, world, origin, targets, trackerFlags);
                trackedWorld = world;
                nextTrackScan = GetTickCount() + 350;
            }
        } else {
            trackerFlags = 0;
        }
        AcquireSRWLockExclusive(&trackerLock);
        trackerState = (TrackerState){controller, pawn, world, trackerFlags, {targets[0], targets[1]}};
        ReleaseSRWLockExclusive(&trackerLock);
        trackedFlags = trackerFlags;
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
            fprintf(output, "2|%lu|%llu|%d|%u|%s|%s|%d|%s", pid, (unsigned long long)now, ready, mask, session, ack, applied, actionError);
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
