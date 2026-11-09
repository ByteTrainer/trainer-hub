#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <limits.h>
#include "MinHook.h"

// RVAs and field offsets are for Steam build 15013637, checked before enabling hooks.
enum {
    Health = 1, Stamina = 2, Filter = 4, Resources = 8,
    Ammo = 16, NoReload = 32, Stealth = 64, All = 127
};
enum {
    PlayerStats = 0x40, PlayerDead = 0xa8,
    BattleInventoryPreview = 0x30, BattlePlayer = 0x40, BattleInventory = 0x58,
    BattleWeapon = 0x68, BattleReloading = 0x90, MovementBattle = 0xa8,
    AttributeKind = 0x18, AttributeValue = 0x24, InventoryOwner = 0x10,
    WeaponRounds = 0x78, TrackedTarget = 0x20
};
typedef const void *Method;
typedef struct { float x, y, z; } Vector3;
typedef void (*Action)(void *, Method);
typedef int (*ReadInt)(void *, Method);
typedef int (*ItemAmount)(void *, void *, Method);
typedef int (*RemoveItem)(void *, void *, int, void *, Method);
typedef void *(*Getter)(void *, Method);
typedef void *(*Lookup)(void *, int, Method);
typedef void (*SetValue)(void *, float, Method);
typedef float (*ReadFloat)(void *, Method);
typedef int (*ReloadWeapon)(void *, int, Method);

static Action originalDestroy, originalUpdate, originalMovement, originalReload, stopReload;
static Getter componentGameObject, weaponAmmo;
static Lookup findAttribute;
static ReadFloat attributeMax;
static ReadInt weaponMax, originalAvailableAmmo;
static ReloadWeapon reloadWeapon;
static SetValue originalSetAttribute;
static ItemAmount originalTotal;
static RemoveItem originalRemove;
static bool (*originalCraft)(void *, Method);
static void (*originalConsumable)(void *, bool, Method);
static void (*originalAwareness)(void *, void *, Vector3, float, float, float, Method);
static void (*originalMaxAwareness)(void *, void *, Vector3, Method);
static void (*originalDecay)(void *, float, float, Method);
static void *player, *playerObject;
static _Thread_local unsigned spendingResources;
static _Thread_local void *reloadInventory, *reloadAmmo;
static volatile LONG requestedFlags, ready;
static volatile LONG64 lease;

static void *objectField(void *object, size_t offset) { return *(void **)((uint8_t *)object + offset); }
static int intField(void *object, size_t offset) { return *(int *)((uint8_t *)object + offset); }
static bool boolField(void *object, size_t offset) { return *((uint8_t *)object + offset) != 0; }

static uint64_t utcTicks(void) {
    FILETIME time;
    GetSystemTimeAsFileTime(&time);
    return (((uint64_t)time.dwHighDateTime << 32) | time.dwLowDateTime) + 504911232000000000ULL;
}

static unsigned activeFlags(void) {
    unsigned flags = (unsigned)InterlockedCompareExchange(&requestedFlags, 0, 0);
    return flags && (uint64_t)InterlockedCompareExchange64(&lease, 0, 0) > utcTicks() ? flags : 0;
}

static bool livePlayer(void) {
    return player && !boolField(player, PlayerDead);
}

static void playerDestroy(void *self, Method method) {
    if (self == player) {
        player = playerObject = NULL;
        InterlockedExchange(&ready, 0);
    }
    originalDestroy(self, method);
}

static void setAttribute(void *self, float value, Method method) {
    unsigned flags = activeFlags() & (Health | Stamina | Filter);
    if (flags && livePlayer()) {
        unsigned kind = (unsigned)intField(self, AttributeKind);
        void *sheet = objectField(player, PlayerStats);
        if (sheet && kind <= 2 && (flags & (1u << kind)) && findAttribute(sheet, (int)kind, NULL) == self)
            value = attributeMax(self, NULL);
    }
    originalSetAttribute(self, value, method);
}

static void refillAttributes(unsigned flags) {
    void *sheet = objectField(player, PlayerStats);
    if (!sheet) return;
    for (int kind = 0; kind < 3; ++kind) {
        if (!(flags & (1u << kind))) continue;
        void *attribute = findAttribute(sheet, kind, NULL);
        if (attribute) {
            float maximum = attributeMax(attribute, NULL);
            if (*(float *)((uint8_t *)attribute + AttributeValue) < maximum)
                originalSetAttribute(attribute, maximum, NULL);
        }
    }
}

static void *localWeapon(void *battle) {
    return livePlayer() && objectField(battle, BattlePlayer) == player
        ? objectField(battle, BattleWeapon) : NULL;
}

static bool observePlayer(void *battle) {
    if (!battle || boolField(battle, BattleInventoryPreview)) return false;
    void *candidate = objectField(battle, BattlePlayer);
    if (!candidate || !objectField(candidate, PlayerStats)) return false;
    if (candidate != player) {
        player = candidate;
        playerObject = componentGameObject(candidate, NULL);
    }
    bool live = livePlayer();
    InterlockedExchange(&ready, live);
    return live;
}

static void movementUpdate(void *self, Method method) {
    if (observePlayer(objectField(self, MovementBattle))) {
        unsigned flags = activeFlags() & (Health | Stamina | Filter);
        if (flags) refillAttributes(flags);
    }
    originalMovement(self, method);
}

static void battleUpdate(void *self, Method method) {
    if (observePlayer(self)) {
        unsigned flags = activeFlags();
        if (flags & NoReload) {
            void *weapon = localWeapon(self);
            if (weapon && weaponAmmo(weapon, NULL)) {
                if (boolField(self, BattleReloading)) stopReload(self, NULL);
                int maximum = weaponMax(weapon, NULL);
                if (maximum > 0 && intField(weapon, WeaponRounds) < maximum)
                    reloadWeapon(weapon, INT_MAX, NULL);
            }
        }
    }
    originalUpdate(self, method);
}

static int availableAmmo(void *self, Method method) {
    void *weapon = (activeFlags() & Ammo) ? localWeapon(self) : NULL;
    if (weapon && weaponAmmo(weapon, NULL)) return INT_MAX;
    return originalAvailableAmmo(self, method);
}

static void battleReload(void *self, Method method) {
    void *weapon = (activeFlags() & Ammo) ? localWeapon(self) : NULL;
    if (!weapon || !weaponAmmo(weapon, NULL)) {
        originalReload(self, method);
        return;
    }
    void *previousInventory = reloadInventory, *previousAmmo = reloadAmmo;
    reloadInventory = objectField(self, BattleInventory);
    reloadAmmo = weaponAmmo(weapon, NULL);
    originalReload(self, method);
    reloadInventory = previousInventory;
    reloadAmmo = previousAmmo;
}

static int totalAmount(void *inventory, void *item, Method method) {
    if (inventory == reloadInventory && item && item == reloadAmmo) return INT_MAX;
    return originalTotal(inventory, item, method);
}

static int removeItem(void *inventory, void *item, int count, void *source, Method method) {
    if (count > 0 && item) {
        if (inventory == reloadInventory && item == reloadAmmo) return 0;
        if (spendingResources && playerObject && objectField(inventory, InventoryOwner) == playerObject &&
            originalTotal(inventory, item, NULL) >= count)
            return 0; // Native result is the amount that could not be removed.
    }
    return originalRemove(inventory, item, count, source, method);
}

static bool craft(void *self, Method method) {
    bool preserve = livePlayer() && (activeFlags() & Resources);
    spendingResources += preserve;
    bool result = originalCraft(self, method);
    spendingResources -= preserve;
    return result;
}

static void consumable(void *self, bool success, Method method) {
    bool preserve = success && livePlayer() && (activeFlags() & Resources);
    spendingResources += preserve;
    originalConsumable(self, success, method);
    spendingResources -= preserve;
}

static bool hidden(void *target) {
    return target && target == playerObject && livePlayer() && (activeFlags() & Stealth);
}

static void awareness(void *self, void *target, Vector3 position, float intensity, float rate, float minimum, Method method) {
    if (!hidden(target)) originalAwareness(self, target, position, intensity, rate, minimum, method);
}

static void maximumAwareness(void *self, void *target, Vector3 position, Method method) {
    if (!hidden(target)) originalMaxAwareness(self, target, position, method);
}

static void decay(void *self, float delay, float amount, Method method) {
    if (hidden(objectField(self, TrackedTarget))) {
        delay = -1;
        amount = 1000000;
    }
    originalDecay(self, delay, amount, method);
}

static bool verifyAssembly(HMODULE assembly) {
    static const uint8_t expected[32] = {
        0xb6, 0xac, 0x92, 0xda, 0x56, 0x60, 0x58, 0x07, 0x49, 0xac, 0xfa, 0x6c, 0x1f, 0xdc, 0x47, 0x16,
        0xad, 0x3b, 0xc8, 0xb1, 0xb3, 0xac, 0x09, 0xb9, 0xf3, 0x3d, 0xc6, 0x44, 0x6d, 0x8b, 0x96, 0xbe
    };
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(assembly, path, MAX_PATH);
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
    ok = BCryptFinishHash(hash, digest, sizeof(digest), 0) >= 0 && memcmp(digest, expected, sizeof(expected)) == 0;
cleanup:
    if (hash) BCryptDestroyHash(hash);
    if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
    CloseHandle(file);
    return ok;
}

static bool installHooks(HMODULE assembly) {
    uint8_t *base = (uint8_t *)assembly;
    const struct { size_t rva; void *detour; void **original; } hooks[] = {
        {0x4ff310, playerDestroy, (void **)&originalDestroy},
        {0x4d1860, setAttribute, (void **)&originalSetAttribute},
        {0x5256d0, battleUpdate, (void **)&originalUpdate},
        {0x4f9aa0, movementUpdate, (void **)&originalMovement},
        {0x5258c0, availableAmmo, (void **)&originalAvailableAmmo},
        {0x524ca0, battleReload, (void **)&originalReload},
        {0x5124e0, totalAmount, (void **)&originalTotal},
        {0x5127d0, removeItem, (void **)&originalRemove},
        {0x538260, craft, (void **)&originalCraft},
        {0x50da90, consumable, (void **)&originalConsumable},
        {0x541c20, awareness, (void **)&originalAwareness},
        {0x541950, maximumAwareness, (void **)&originalMaxAwareness},
        {0x54e510, decay, (void **)&originalDecay}
    };
    componentGameObject = (Getter)(base + 0x29a88c0);
    findAttribute = (Lookup)(base + 0x4e4980);
    attributeMax = (ReadFloat)(base + 0x4d1740);
    weaponAmmo = (Getter)(base + 0x554410);
    weaponMax = (ReadInt)(base + 0x554430);
    reloadWeapon = (ReloadWeapon)(base + 0x554110);
    stopReload = (Action)(base + 0x5251c0);
    if (MH_Initialize() != MH_OK) return false;
    for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); ++i) {
        if (MH_CreateHook(base + hooks[i].rva, hooks[i].detour, hooks[i].original) != MH_OK) {
            MH_Uninitialize();
            return false;
        }
    }
    if (MH_EnableHook(MH_ALL_HOOKS) == MH_OK) return true;
    MH_Uninitialize();
    return false;
}

static void readSettings(const wchar_t *file, DWORD pid, uint64_t now) {
    char buffer[128], extra;
    unsigned owner, flags;
    unsigned long long expiry;
    DWORD size = 0;
    HANDLE input = CreateFileW(file, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                              NULL, OPEN_EXISTING, 0, NULL);
    if (input == INVALID_HANDLE_VALUE) return;
    bool ok = ReadFile(input, buffer, sizeof(buffer) - 1, &size, NULL);
    CloseHandle(input);
    if (!ok || size == sizeof(buffer) - 1) return;
    buffer[size] = 0;
    if (sscanf(buffer, "%u|%llu|%u %c", &owner, &expiry, &flags, &extra) != 3 ||
        owner != pid || expiry <= now || expiry - now > 50000000ULL || flags > All) {
        expiry = flags = 0;
    }
    InterlockedExchange(&requestedFlags, 0);
    InterlockedExchange64(&lease, (LONG64)expiry);
    InterlockedExchange(&requestedFlags, (LONG)flags);
}

static void publish(const wchar_t *pending, const wchar_t *status, DWORD pid, uint64_t now) {
    char buffer[128];
    int playing = InterlockedCompareExchange(&ready, 0, 0);
    int length = snprintf(buffer, sizeof(buffer), "1|%lu|%llu|%d|%u", pid,
                          (unsigned long long)now, playing, playing ? activeFlags() : 0);
    HANDLE file = CreateFileW(pending, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return;
    DWORD written = 0;
    bool ok = WriteFile(file, buffer, (DWORD)length, &written, NULL) && written == (DWORD)length;
    CloseHandle(file);
    if (ok) MoveFileExW(pending, status, MOVEFILE_REPLACE_EXISTING);
}

static DWORD WINAPI run(void *unused) {
    (void)unused;
    HMODULE runtime;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                            (LPCWSTR)&run, &runtime)) return 0;
    HMODULE assembly = GetModuleHandleW(L"GameAssembly.dll");
    if (!assembly || !verifyAssembly(assembly) || !installHooks(assembly)) return 0;
    wchar_t folder[MAX_PATH], settings[MAX_PATH], pending[MAX_PATH], status[MAX_PATH];
    DWORD length = GetTempPathW(MAX_PATH, folder);
    if (!length || length >= MAX_PATH - 64) return 0;
    wcscat(folder, L"TrainerHub-2475440");
    CreateDirectoryW(folder, NULL);
    swprintf(settings, MAX_PATH, L"%ls\\settings.txt", folder);
    swprintf(status, MAX_PATH, L"%ls\\status.txt", folder);
    DWORD pid = GetCurrentProcessId();
    swprintf(pending, MAX_PATH, L"%ls\\status-%lu.tmp", folder, pid);
    for (;;) {
        uint64_t now = utcTicks();
        readSettings(settings, pid, now);
        publish(pending, status, pid, now);
        Sleep(150);
    }
}

__declspec(dllexport) void ByteTrainerRuntime(void) {}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved) {
    (void)module;
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        HANDLE thread = CreateThread(NULL, 0, run, NULL, 0, NULL);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
